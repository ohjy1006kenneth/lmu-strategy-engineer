using System.Collections.Immutable;
using LmuStrategy.Domain;

namespace LmuStrategy.Strategy;

/// <summary>
/// Shared candidate/editor validation and scoring. Candidate time fields are display caches and are
/// never used as model inputs or scores. This class never mutates or repairs a candidate.
/// Mounted start tyres are already outside the supplied remaining inventory; only replacements debit it.
/// </summary>
public sealed class PlanEvaluator(IPlanPredictionModel model)
{
    private readonly IPlanPredictionModel model = model ?? throw new ArgumentNullException(nameof(model));

    public PlanEvaluation EvaluatePlan(StrategyInput input, RacePlan plan, RacePlan? recommendedPlan = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(plan);
        var result = new Evaluation(model, input, plan).Run();
        if (recommendedPlan is null) return result;
        var recommended = new Evaluation(model, input, recommendedPlan).Run();
        if (!recommended.Valid)
            return result with { Warnings = result.Warnings.Add("Recommended plan is invalid; comparison is unavailable.") };
        return result.Valid ? result with
        {
            DeltaVsRecommended = new(result.PredictedTotalTime!.Value.Value - recommended.PredictedTotalTime!.Value.Value)
        } : result;
    }

    private sealed class Evaluation(IPlanPredictionModel model, StrategyInput input, RacePlan plan)
    {
        // Numerical equality only; this is not an extra strategic reserve or capacity allowance.
        private const double NumericTolerance = 1e-9;
        private readonly List<PlanViolation> violations = [];
        private readonly List<string> warnings = [];
        private readonly List<PlanScoreTerm> score = [];
        private readonly List<DryTyreStock> inventory = [];
        private bool inventoryKnown;
        private ConfidenceLevel confidence = ConfidenceLevel.High;
        private FuelLitres? finishFuel;
        private VePercent? finishVe;

        private void Fail(ViolationCode code, string path, string message) => violations.Add(new(code, path, message));
        private static bool Equal(double left, double right) => Math.Abs(left - right) <= NumericTolerance;
        private static bool Verified(CapabilityStatus status) => status is CapabilityStatus.VerifiedRest or CapabilityStatus.VerifiedSharedMemory;
        private CapabilityStatus Capability(CapabilityId id) => input.LmuCapabilities.Entries.IsDefault
            ? CapabilityStatus.Unavailable : input.LmuCapabilities.StatusOf(id);

        private bool Evidence(ModelEvidence evidence, string path)
        {
            if (evidence.Source == ModelSource.Unavailable || evidence.Confidence == ConfidenceLevel.Unavailable)
            {
                Fail(ViolationCode.MissingPrediction, path, "Prediction has unavailable evidence.");
                return false;
            }
            if (evidence.Source is ModelSource.PersonalMeasured or ModelSource.PersonalFitted && evidence.SampleCount == 0)
            {
                Fail(ViolationCode.MissingPrediction, path, "Personal predictions require observed samples.");
                return false;
            }
            if (evidence.Source is ModelSource.VerifiedLmu or ModelSource.ValidatedBuildFallback &&
                !CurrentIdentity(evidence))
            {
                Fail(ViolationCode.MissingCapability, path, "Prediction is not supported by the current integration identity.");
                return false;
            }
            var supportedConfidence = evidence.Confidence;
            if (evidence.Source is ModelSource.SameCarFieldFallback or ModelSource.SameClassFieldFallback or
                ModelSource.ValidatedBuildFallback or ModelSource.SyntheticSimulationFixture || evidence.ExtrapolationLaps?.Value > 0)
            {
                supportedConfidence = ConfidenceLevel.Low;
                warnings.Add($"{path}: fallback, fixture or extrapolated prediction; confidence is at most Low.");
            }
            confidence = (ConfidenceLevel)Math.Min((int)confidence, (int)supportedConfidence);
            return true;
        }

        private bool CurrentIdentity(ModelEvidence evidence)
        {
            var identity = input.LmuCapabilities.Identity;
            return identity.GameBuild is not null && identity.AdapterVersion is not null && identity.SchemaVersion is not null &&
                evidence.Identity == identity;
        }

        private bool Constraint<T>(Sourced<T>? value, CapabilityId id, string path, bool allowCached = false)
        {
            var status = Capability(id);
            if (value is null || !(Verified(status) || allowCached && status == CapabilityStatus.Cached) ||
                value.Evidence.Source != ModelSource.VerifiedLmu || !CurrentIdentity(value.Evidence))
            {
                Fail(ViolationCode.MissingCapability, path, "Current verified constraint is unavailable.");
                return false;
            }
            return Evidence(value.Evidence, path);
        }

        internal PlanEvaluation Run()
        {
            if (!Structure()) return Result();
            var constraints = input.SessionConstraints;
            bool fuelCapacity = Constraint(constraints.FuelCapacity, CapabilityId.FuelCapacity, "constraints.fuelCapacity");
            bool veCapacity = Constraint(constraints.VeCapacity, CapabilityId.VeCapacity, "constraints.veCapacity");
            bool legalCompounds = Constraint(constraints.LegalCompounds, CapabilityId.LegalCompounds, "constraints.legalCompounds");
            if (legalCompounds && constraints.LegalCompounds!.Value.IsDefault)
            {
                Fail(ViolationCode.UnsupportedInput, "constraints.legalCompounds", "Default collection is not a known list.");
                legalCompounds = false;
            }
            bool legalServices = plan.Pits.Length == 0 || Constraint(constraints.LegalTyreServices,
                CapabilityId.TyreServiceOptions, "constraints.legalTyreServices");
            if (plan.Pits.Length > 0 && legalServices && constraints.LegalTyreServices!.Value.IsDefault)
            {
                Fail(ViolationCode.UnsupportedInput, "constraints.legalTyreServices", "Default collection is not a known list.");
                legalServices = false;
            }
            LoadInventory();
            if (input.UserPreferences.VeReserve.Value < 0)
                Fail(ViolationCode.Energy, "preferences.veReserve", "Reserve must be nonnegative.");
            var projection = model.ProjectRaceFinish(input);
            if (projection is null)
                Fail(ViolationCode.MissingPrediction, "raceFinish", "Race projection is unavailable; finish coverage cannot be certified.");
            else
            {
                Evidence(projection.Evidence, "raceFinish");
                long required = (long)projection.Value.Value + input.UserPreferences.BufferLaps.Value;
                if (projection.Value.Value < input.RaceState.CompletedLaps.Value || required != plan.Stints[^1].EndLap.Value)
                    Fail(ViolationCode.FinishBuffer, "stints", "Last boundary must equal projected race finish plus requested buffer.");
            }
            for (int i = 0; i < plan.Stints.Length; i++)
            {
                var stint = plan.Stints[i];
                string path = $"stints[{i}]";
                CheckLoad(stint.StartFuel.Value, stint.StartVe.Value, path, fuelCapacity, veCapacity);
                CheckTyres(stint.TyreStart, path + ".tyreStart", legalCompounds);
                CheckTyres(stint.TyreEnd, path + ".tyreEnd", legalCompounds);
                if (i == 0)
                {
                    if (stint.TyreStart != input.VehicleState.Tyres)
                        Fail(ViolationCode.TyreContinuity, path, "Start tyres must match the mounted vehicle tyres.");
                    if (input.RaceState.CompletedLaps.Value > 0 &&
                        (input.VehicleState.Fuel is null || input.VehicleState.Ve is null || input.VehicleState.FuelRatio is null ||
                        !Equal(stint.StartFuel.Value, input.VehicleState.Fuel.Value.Value) ||
                        !Equal(stint.StartVe.Value, input.VehicleState.Ve.Value.Value) || stint.FuelRatio != input.VehicleState.FuelRatio))
                        Fail(ViolationCode.Continuity, path, "Remaining plan must start from observed Fuel, VE and ratio.");
                }
                var prediction = model.PredictStint(input, stint);
                if (prediction is null)
                {
                    Fail(ViolationCode.MissingPrediction, path, "Stint conditions/energy/pace/tyres are unsupported.");
                    continue;
                }
                Evidence(prediction.FuelPerLap.Evidence, path + ".fuel");
                Evidence(prediction.VePerLap.Evidence, path + ".ve");
                Evidence(prediction.TyreEvidence, path + ".tyres");
                if (!Equal(stint.TargetFuelPerLap.Value, prediction.FuelPerLap.Value.Value) ||
                    !Equal(stint.TargetVePerLap.Value, prediction.VePerLap.Value.Value))
                    Fail(ViolationCode.Energy, path, "Candidate target rates differ from the supported consumption prediction.");
                ValidateTyrePrediction(stint, prediction.TyreEnd, path);
                ScoreStint(stint, prediction, path);
                double arrivalFuel = stint.StartFuel.Value - prediction.FuelPerLap.Value.Value * stint.LapCount.Value;
                double arrivalVe = stint.StartVe.Value - prediction.VePerLap.Value.Value * stint.LapCount.Value;
                if (!double.IsFinite(arrivalFuel) || !double.IsFinite(arrivalVe) ||
                    arrivalFuel + NumericTolerance < input.UserPreferences.FuelReserve.Value ||
                    arrivalVe + NumericTolerance < input.UserPreferences.VeReserve.Value ||
                    arrivalFuel < -NumericTolerance || arrivalVe < -NumericTolerance)
                {
                    Fail(ViolationCode.Energy, path, "Stint consumption exceeds available energy or required reserve.");
                    continue;
                }
                // Roundoff within 1e-9 of zero is represented as zero; no infeasible energy is repaired.
                arrivalFuel = Math.Max(0, arrivalFuel);
                arrivalVe = Math.Max(0, arrivalVe);
                if (i < plan.Pits.Length)
                    EvaluatePit(i, arrivalFuel, arrivalVe, legalServices, legalCompounds, fuelCapacity, veCapacity);
                else
                {
                    finishFuel = new(arrivalFuel);
                    finishVe = new(arrivalVe);
                    if (!Equal(plan.FinishFuel.Value, arrivalFuel) || !Equal(plan.FinishVe.Value, arrivalVe))
                        Fail(ViolationCode.Energy, "finish", "Declared finish energy differs from predicted finish energy.");
                }
            }
            CheckClaimedInventory();
            return Result();
        }

        private bool Structure()
        {
            if (plan.Stints.IsDefaultOrEmpty || plan.Pits.IsDefault || plan.Pits.Length != plan.Stints.Length - 1)
            {
                Fail(ViolationCode.Order, "plan", "A plan needs nonempty stints and exactly one pit between adjacent stints.");
                return false;
            }
            if (!Enum.IsDefined(plan.StrategyMode))
                Fail(ViolationCode.UnsupportedInput, "strategyMode", "Unknown strategy mode.");
            int boundary = input.RaceState.CompletedLaps.Value;
            for (int i = 0; i < plan.Stints.Length; i++)
            {
                var s = plan.Stints[i];
                if (s is null || s.Index != i || s.StartLap.Value != boundary || s.EndLap.Value <= s.StartLap.Value)
                {
                    Fail(ViolationCode.Order, $"stints[{i}]", "Stints must have sequential indices and contiguous increasing boundaries from current lap.");
                    return false;
                }
                boundary = s.EndLap.Value;
                if (s.TyreStart is null || s.TyreEnd is null)
                {
                    Fail(ViolationCode.UnsupportedInput, $"stints[{i}]", "Per-corner tyre state is required.");
                    return false;
                }
                if (Corners(s.TyreStart).Concat(Corners(s.TyreEnd)).Any(c =>
                    c.Value is null || c.Value.Compound is null || c.Value.RemainingLife is null || c.Value.Age is null))
                {
                    Fail(ViolationCode.MissingPrediction, $"stints[{i}]", "Every corner requires compound, life and age before prediction.");
                    return false;
                }
                if (i < plan.Pits.Length)
                {
                    var pit = plan.Pits[i];
                    if (pit is null || pit.AfterStint != i || pit.PitLap != s.EndLap || pit.Replacements is null)
                    {
                        Fail(ViolationCode.Order, $"pits[{i}]", "Pits must follow the matching stint at its end boundary with explicit replacements.");
                        return false;
                    }
                }
            }
            return true;
        }

        private void CheckLoad(double fuel, double ve, string path, bool fuelCapacity, bool veCapacity)
        {
            if (fuelCapacity && fuel > input.SessionConstraints.FuelCapacity!.Value.Value + NumericTolerance ||
                veCapacity && ve > input.SessionConstraints.VeCapacity!.Value.Value + NumericTolerance)
                Fail(ViolationCode.Capacity, path, "Energy load exceeds verified capacity.");
        }

        private static IEnumerable<(WheelPosition Wheel, T Value)> Corners<T>(PerWheel<T> value)
        {
            yield return (WheelPosition.FL, value.FL);
            yield return (WheelPosition.FR, value.FR);
            yield return (WheelPosition.RL, value.RL);
            yield return (WheelPosition.RR, value.RR);
        }

        private void CheckTyres(PerWheel<TyreState> tyres, string path, bool legalCompounds)
        {
            foreach (var (wheel, tyre) in Corners(tyres))
            {
                if (tyre is null || tyre.Compound is null || tyre.RemainingLife is null || tyre.Age is null)
                {
                    Fail(ViolationCode.MissingPrediction, $"{path}.{wheel}", "Compound, remaining life and age are required.");
                    continue;
                }
                CheckCompound(tyre.Compound, $"{path}.{wheel}", legalCompounds);
            }
        }

        private void CheckCompound(TyreCompound compound, string path, bool legalCompounds)
        {
            if (!Enum.IsDefined(compound.Surface) || string.IsNullOrWhiteSpace(compound.Id) ||
                legalCompounds && !input.SessionConstraints.LegalCompounds!.Value.Contains(compound))
                Fail(ViolationCode.IllegalCompound, path, "Compound is not legal in this event.");
        }

        private void ValidateTyrePrediction(Stint stint, PerWheel<TyreState> predicted, string path)
        {
            if (predicted != stint.TyreEnd)
                Fail(ViolationCode.TyreContinuity, path, "Declared end tyres differ from modeled end tyres.");
            foreach (var pair in Corners(stint.TyreStart).Zip(Corners(predicted)))
            {
                var start = pair.First.Value;
                var end = pair.Second.Value;
                if (start is null || end is null || start.Compound != end.Compound || end.RemainingLife is null ||
                    start.RemainingLife is null || end.RemainingLife.Value.Value > start.RemainingLife.Value.Value ||
                    start.Age is null || end.Age is null || (long)start.Age.Value.Value + stint.LapCount.Value != end.Age.Value.Value)
                    Fail(ViolationCode.TyreContinuity, path, "Tyres cannot change compound, rejuvenate or skip age within a stint.");
            }
        }

        private void ScoreStint(Stint stint, StintPrediction prediction, string path)
        {
            if (prediction.Laps.IsDefault || prediction.Laps.Length != stint.LapCount.Value)
            {
                Fail(ViolationCode.MissingPrediction, path, "Every covered lap needs explicit time terms.");
                return;
            }
            for (int j = 0; j < prediction.Laps.Length; j++)
            {
                var lap = prediction.Laps[j];
                if (lap is null)
                {
                    Fail(ViolationCode.MissingPrediction, path, "Lap prediction is missing.");
                    continue;
                }
                TimeEstimate[] terms = [lap.Baseline, lap.FuelMass, lap.Saving, lap.Tyres, lap.RoadWeather, lap.Other];
                if (lap.Baseline is { Expected.Value: <= 0 })
                    Fail(ViolationCode.UnsupportedInput, path, "Baseline race pace must be positive.");
                for (int k = 0; k < terms.Length; k++)
                    AddScore(stint.Index, new(stint.StartLap.Value + j + 1), (ScoreComponent)k, terms[k], path);
                if (terms.All(t => t is not null) && terms.Sum(t => t.Expected.Value) < 0)
                    Fail(ViolationCode.UnsupportedInput, path, "Predicted lap time must be nonnegative.");
            }
        }

        private void AddScore(int stint, Laps? lap, ScoreComponent component, TimeEstimate? estimate, string path)
        {
            if (estimate is null)
            {
                Fail(ViolationCode.MissingPrediction, path, $"Missing explicit {component} term; zero cannot be assumed.");
                return;
            }
            Evidence(estimate.Evidence, $"{path}.{component}");
            score.Add(new(stint, lap, component, estimate));
        }

        private void EvaluatePit(int i, double arrivalFuel, double arrivalVe, bool legalServices,
            bool legalCompounds, bool fuelCapacity, bool veCapacity)
        {
            var pit = plan.Pits[i];
            var next = plan.Stints[i + 1];
            var previous = plan.Stints[i];
            string path = $"pits[{i}]";
            CheckLoad(pit.TargetFuel.Value, pit.TargetVe.Value, path, fuelCapacity, veCapacity);
            if (!Equal(pit.TargetFuel.Value, next.StartFuel.Value) || !Equal(pit.TargetVe.Value, next.StartVe.Value) ||
                pit.FuelRatio != next.FuelRatio)
                Fail(ViolationCode.Continuity, path, "Service targets and ratio must equal the next stint's starting load.");
            double fuelAdded = pit.TargetFuel.Value - arrivalFuel;
            double veAdded = pit.TargetVe.Value - arrivalVe;
            if (fuelAdded < -NumericTolerance || veAdded < -NumericTolerance)
                Fail(ViolationCode.Energy, path, "Service cannot remove Fuel or VE.");
            int dryUsed = 0;
            TyreServicePattern pattern = TyreServicePattern.None;
            var previousCorners = Corners(previous.TyreEnd).ToArray();
            var nextCorners = Corners(next.TyreStart).ToArray();
            int k = 0;
            foreach (var (wheel, replacement) in Corners(pit.Replacements))
            {
                var expected = previousCorners[k].Value;
                if (replacement is not null)
                {
                    pattern |= (TyreServicePattern)(1 << (int)wheel);
                    CheckCompound(replacement.Compound, path, legalCompounds);
                    expected = new(replacement.Compound, new(100), new(0));
                    if (replacement.Compound.Surface == TyreSurface.Dry)
                    {
                        dryUsed++;
                        Debit(replacement.Compound, wheel, path);
                    }
                }
                if (nextCorners[k].Value != expected)
                    Fail(ViolationCode.TyreContinuity, path, $"{wheel} must retain its arrival state or receive the declared new tyre.");
                k++;
            }
            if (legalServices && !input.SessionConstraints.LegalTyreServices!.Value.Contains(pattern))
                Fail(ViolationCode.IllegalService, path, "Replacement corner pattern is not legal in this event.");
            if (pit.DryTyresConsumed.Value != dryUsed)
                Fail(ViolationCode.Inventory, path, "Declared dry charge differs from per-corner replacements; Wet is exempt.");
            if (fuelAdded < -NumericTolerance || veAdded < -NumericTolerance) return;
            var prediction = model.PredictPit(input, new(i, pit.PitLap, new(Math.Max(0, fuelAdded)),
                new(Math.Max(0, veAdded)), pit.FuelRatio, pit.Replacements));
            if (prediction is null)
            {
                Fail(ViolationCode.MissingPrediction, path, "No supported pit estimate or validated fallback for this service request.");
                return;
            }
            TimeEstimate[] terms = [prediction.LaneLoss, prediction.Service, prediction.ColdOutlap];
            for (int j = 0; j < terms.Length; j++)
            {
                var term = terms[j];
                AddScore(i, null, (ScoreComponent)((int)ScoreComponent.PitLane + j), term, path);
                if (term is not null && (term.Expected.Value < 0 ||
                    term.Evidence.Source is not (ModelSource.VerifiedLmu or ModelSource.ValidatedBuildFallback or ModelSource.SyntheticSimulationFixture)))
                    Fail(ViolationCode.MissingPrediction, path, "Pit costs require nonnegative verified, validated-build or explicit fixture estimates.");
                if (term?.Evidence.Source == ModelSource.VerifiedLmu && !Verified(Capability(CapabilityId.PitEstimate)))
                    Fail(ViolationCode.MissingCapability, path, "Verified pit estimate capability is unavailable.");
            }
        }

        private void LoadInventory()
        {
            var stock = input.SessionConstraints.AvailableDryTyres;
            if (stock is null)
            {
                warnings.Add("Dry inventory is unavailable; no dry replacement can be certified.");
                return;
            }
            if (!Constraint(stock, CapabilityId.DryTyreInventory, "constraints.inventory", allowCached: true)) return;
            if (stock.Value.IsDefault || stock.Value.Any(s => s is null || s.RestrictedTo is { } w && !Enum.IsDefined(w)) ||
                stock.Value.GroupBy(s => (s.Compound, s.RestrictedTo)).Any(g => g.Count() > 1))
            {
                Fail(ViolationCode.Inventory, "constraints.inventory", "Inventory must have explicit, unique compound/corner pools.");
                return;
            }
            inventoryKnown = true;
            inventory.AddRange(stock.Value);
        }

        private void Debit(TyreCompound compound, WheelPosition wheel, string path)
        {
            // Restricted stock is used first, preserving flexible stock for other corners.
            int index = inventory.FindIndex(s => s.Compound == compound && s.RestrictedTo == wheel && s.Available.Value > 0);
            if (index < 0) index = inventory.FindIndex(s => s.Compound == compound && s.RestrictedTo is null && s.Available.Value > 0);
            if (!inventoryKnown || index < 0)
            {
                Fail(ViolationCode.Inventory, path, $"No supported dry inventory remains for {wheel}, {compound.Id}.");
                return;
            }
            var entry = inventory[index];
            inventory[index] = new(entry.Compound, new(entry.Available.Value - 1), entry.RestrictedTo);
        }

        private void CheckClaimedInventory()
        {
            if (plan.DryTyresRemaining is not { } claim) return;
            if (!inventoryKnown || claim.IsDefault || claim.Length != inventory.Count ||
                claim.Any(s => s is null) || claim.GroupBy(s => (s.Compound, s.RestrictedTo)).Any(g => g.Count() > 1) ||
                claim.Any(s => !inventory.Contains(s)))
                Fail(ViolationCode.Inventory, "dryTyresRemaining", "Declared remaining inventory differs from the replacement ledger.");
        }

        private PlanEvaluation Result()
        {
            double total = score.Sum(s => s.Estimate.Expected.Value);
            double uncertainty = score.Sum(s => s.Estimate.Uncertainty.Value);
            if (!double.IsFinite(total) || total < 0 || !double.IsFinite(uncertainty))
                Fail(ViolationCode.UnsupportedInput, "score", "Score or uncertainty is outside the supported numeric range.");
            bool valid = violations.Count == 0;
            return new(valid, violations.ToImmutableArray(), valid ? new Seconds(total) : null, null,
                score.ToImmutableArray(), valid ? new Seconds(uncertainty) : null,
                valid ? confidence : ConfidenceLevel.Unavailable, warnings.Distinct().ToImmutableArray(),
                inventoryKnown ? inventory.ToImmutableArray() : null, valid ? finishFuel : null, valid ? finishVe : null);
        }
    }
}
