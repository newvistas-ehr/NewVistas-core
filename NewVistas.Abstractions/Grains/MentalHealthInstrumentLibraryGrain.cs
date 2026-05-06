// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Mental Health Instrument Library grain — singleton reference data for screening instruments.
/// Self-seeds on activation with 9 standard instruments: PHQ-9, GAD-7, AUDIT-C, PCL-5,
/// Columbia Suicide Severity, AUDIT (full), DAST-10, CAGE, CIWA-Ar.
/// Maps to VistA YS MH INSTRUMENT (#601.71) instrument definitions.
/// </summary>
public class MentalHealthInstrumentLibraryGrain : Grain, IMentalHealthInstrumentLibraryGrain
{
    private readonly IPersistentState<MhInstrumentLibraryState> _state;

    public MentalHealthInstrumentLibraryGrain(
        [PersistentState("mhInstrumentLibraryState", "mhInstrumentStore")] IPersistentState<MhInstrumentLibraryState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.Instruments.Count == 0)
        {
            await SeedDemoDataAsync();
        }
        await base.OnActivateAsync(cancellationToken);
    }

    public Task<List<MhInstrumentDefinition>> GetAllInstrumentsAsync()
        => Task.FromResult(_state.State.Instruments);

    public Task<MhInstrumentDefinition?> GetInstrumentAsync(string instrumentName)
    {
        MhInstrumentDefinition? definition = _state.State.Instruments
            .FirstOrDefault(i => string.Equals(i.InstrumentName, instrumentName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(definition);
    }

    public async Task AddInstrumentAsync(MhInstrumentDefinition instrument)
    {
        MhInstrumentDefinition? existing = _state.State.Instruments
            .FirstOrDefault(i => string.Equals(i.InstrumentName, instrument.InstrumentName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _state.State.Instruments.Remove(existing);
        }
        _state.State.Instruments.Add(instrument);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SeedDemoDataAsync()
    {
        _state.State.Instruments.Clear();

        // 1. PHQ-9 — Patient Health Questionnaire (Depression)
        List<string> phq9Options = new() { "Not at all (0)", "Several days (1)", "More than half the days (2)", "Nearly every day (3)" };
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "PHQ-9",
            Description = "Patient Health Questionnaire-9: a validated 9-item depression screening tool",
            ItemCount = 9,
            MaxScore = 27,
            PositiveScreenThreshold = 10,
            ClinicalDomain = "DEPRESSION",
            Frequency = "EVERY_VISIT",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 4, Interpretation = "MINIMAL", Recommendation = "No treatment indicated; monitor" },
                new() { MinScore = 5, MaxScore = 9, Interpretation = "MILD", Recommendation = "Watchful waiting; repeat PHQ-9 at follow-up" },
                new() { MinScore = 10, MaxScore = 14, Interpretation = "MODERATE", Recommendation = "Consider counseling or pharmacotherapy" },
                new() { MinScore = 15, MaxScore = 19, Interpretation = "MODERATELY SEVERE", Recommendation = "Active treatment with pharmacotherapy and/or psychotherapy" },
                new() { MinScore = 20, MaxScore = 27, Interpretation = "SEVERE", Recommendation = "Immediate initiation of pharmacotherapy and psychotherapy; consider referral to specialist" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "Little interest or pleasure in doing things", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 2, QuestionText = "Feeling down, depressed, or hopeless", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 3, QuestionText = "Trouble falling or staying asleep, or sleeping too much", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 4, QuestionText = "Feeling tired or having little energy", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 5, QuestionText = "Poor appetite or overeating", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 6, QuestionText = "Feeling bad about yourself — or that you are a failure or have let yourself or your family down", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 7, QuestionText = "Trouble concentrating on things, such as reading the newspaper or watching television", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 8, QuestionText = "Moving or speaking so slowly that other people could have noticed? Or the opposite — being so fidgety or restless that you have been moving around a lot more than usual", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options },
                new() { ItemNumber = 9, QuestionText = "Thoughts that you would be better off dead or of hurting yourself in some way", MinValue = 0, MaxValue = 3, ResponseOptions = phq9Options }
            }
        });

        // 2. GAD-7 — Generalized Anxiety Disorder
        List<string> gad7Options = new() { "Not at all (0)", "Several days (1)", "More than half the days (2)", "Nearly every day (3)" };
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "GAD-7",
            Description = "Generalized Anxiety Disorder 7-item scale: a validated anxiety screening tool",
            ItemCount = 7,
            MaxScore = 21,
            PositiveScreenThreshold = 10,
            ClinicalDomain = "ANXIETY",
            Frequency = "EVERY_VISIT",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 4, Interpretation = "MINIMAL", Recommendation = "No treatment indicated; monitor" },
                new() { MinScore = 5, MaxScore = 9, Interpretation = "MILD", Recommendation = "Watchful waiting; repeat GAD-7 at follow-up" },
                new() { MinScore = 10, MaxScore = 14, Interpretation = "MODERATE", Recommendation = "Consider counseling or pharmacotherapy" },
                new() { MinScore = 15, MaxScore = 21, Interpretation = "SEVERE", Recommendation = "Active treatment with pharmacotherapy and/or psychotherapy" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "Feeling nervous, anxious, or on edge", MinValue = 0, MaxValue = 3, ResponseOptions = gad7Options },
                new() { ItemNumber = 2, QuestionText = "Not being able to stop or control worrying", MinValue = 0, MaxValue = 3, ResponseOptions = gad7Options },
                new() { ItemNumber = 3, QuestionText = "Worrying too much about different things", MinValue = 0, MaxValue = 3, ResponseOptions = gad7Options },
                new() { ItemNumber = 4, QuestionText = "Trouble relaxing", MinValue = 0, MaxValue = 3, ResponseOptions = gad7Options },
                new() { ItemNumber = 5, QuestionText = "Being so restless that it is hard to sit still", MinValue = 0, MaxValue = 3, ResponseOptions = gad7Options },
                new() { ItemNumber = 6, QuestionText = "Becoming easily annoyed or irritable", MinValue = 0, MaxValue = 3, ResponseOptions = gad7Options },
                new() { ItemNumber = 7, QuestionText = "Feeling afraid, as if something awful might happen", MinValue = 0, MaxValue = 3, ResponseOptions = gad7Options }
            }
        });

        // 3. AUDIT-C — Alcohol Use Disorders Identification Test (Consumption)
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "AUDIT-C",
            Description = "Alcohol Use Disorders Identification Test-Consumption: a 3-item alcohol screening tool",
            ItemCount = 3,
            MaxScore = 12,
            PositiveScreenThreshold = 4,
            ClinicalDomain = "SUBSTANCE_USE",
            Frequency = "ANNUAL",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 3, Interpretation = "NEGATIVE", Recommendation = "Low-risk drinking; no intervention needed" },
                new() { MinScore = 4, MaxScore = 12, Interpretation = "POSITIVE", Recommendation = "Positive screen; conduct full AUDIT and brief intervention" }
            },
            Items = new List<MhInstrumentItem>
            {
                new()
                {
                    ItemNumber = 1,
                    QuestionText = "How often do you have a drink containing alcohol?",
                    MinValue = 0, MaxValue = 4,
                    ResponseOptions = new() { "Never (0)", "Monthly or less (1)", "2-4 times a month (2)", "2-3 times a week (3)", "4 or more times a week (4)" }
                },
                new()
                {
                    ItemNumber = 2,
                    QuestionText = "How many drinks containing alcohol do you have on a typical day when you are drinking?",
                    MinValue = 0, MaxValue = 4,
                    ResponseOptions = new() { "1 or 2 (0)", "3 or 4 (1)", "5 or 6 (2)", "7 to 9 (3)", "10 or more (4)" }
                },
                new()
                {
                    ItemNumber = 3,
                    QuestionText = "How often do you have 6 or more drinks on one occasion?",
                    MinValue = 0, MaxValue = 4,
                    ResponseOptions = new() { "Never (0)", "Less than monthly (1)", "Monthly (2)", "Weekly (3)", "Daily or almost daily (4)" }
                }
            }
        });

        // 4. PCL-5 — PTSD Checklist for DSM-5
        List<string> pcl5Options = new() { "Not at all (0)", "A little bit (1)", "Moderately (2)", "Quite a bit (3)", "Extremely (4)" };
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "PCL-5",
            Description = "PTSD Checklist for DSM-5: a 20-item self-report measure of PTSD symptoms",
            ItemCount = 20,
            MaxScore = 80,
            PositiveScreenThreshold = 31,
            ClinicalDomain = "PTSD",
            Frequency = "QUARTERLY",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 30, Interpretation = "SUBTHRESHOLD", Recommendation = "Below threshold; monitor symptoms" },
                new() { MinScore = 31, MaxScore = 80, Interpretation = "PROBABLE PTSD", Recommendation = "Probable PTSD; refer for diagnostic evaluation and evidence-based treatment" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "Repeated, disturbing, and unwanted memories of the stressful experience", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 2, QuestionText = "Repeated, disturbing dreams of the stressful experience", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 3, QuestionText = "Suddenly feeling or acting as if the stressful experience were actually happening again", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 4, QuestionText = "Feeling very upset when something reminded you of the stressful experience", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 5, QuestionText = "Having strong physical reactions when something reminded you of the stressful experience", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 6, QuestionText = "Avoiding memories, thoughts, or feelings related to the stressful experience", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 7, QuestionText = "Avoiding external reminders of the stressful experience", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 8, QuestionText = "Trouble remembering important parts of the stressful experience", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 9, QuestionText = "Having strong negative beliefs about yourself, other people, or the world", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 10, QuestionText = "Blaming yourself or someone else for the stressful experience or what happened after it", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 11, QuestionText = "Having strong negative feelings such as fear, horror, anger, guilt, or shame", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 12, QuestionText = "Loss of interest in activities that you used to enjoy", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 13, QuestionText = "Feeling distant or cut off from other people", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 14, QuestionText = "Trouble experiencing positive feelings", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 15, QuestionText = "Irritable behavior, angry outbursts, or acting aggressively", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 16, QuestionText = "Taking too many risks or doing things that could cause you harm", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 17, QuestionText = "Being 'superalert' or watchful or on guard", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 18, QuestionText = "Feeling jumpy or easily startled", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 19, QuestionText = "Having difficulty concentrating", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options },
                new() { ItemNumber = 20, QuestionText = "Trouble falling or staying asleep", MinValue = 0, MaxValue = 4, ResponseOptions = pcl5Options }
            }
        });

        // 5. Columbia Suicide Severity Rating Scale (C-SSRS)
        List<string> cssrsOptions = new() { "No (0)", "Yes (1)" };
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "Columbia Suicide Severity",
            Description = "Columbia-Suicide Severity Rating Scale (C-SSRS): a 6-item suicide risk screening tool",
            ItemCount = 6,
            MaxScore = 6,
            PositiveScreenThreshold = 1,
            ClinicalDomain = "SUICIDE_RISK",
            Frequency = "EVERY_VISIT",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 0, Interpretation = "NO RISK", Recommendation = "No current suicidal ideation identified" },
                new() { MinScore = 1, MaxScore = 2, Interpretation = "LOW", Recommendation = "Monitor; provide safety information and crisis resources" },
                new() { MinScore = 3, MaxScore = 4, Interpretation = "MODERATE", Recommendation = "Initiate safety planning; consider mental health referral" },
                new() { MinScore = 5, MaxScore = 6, Interpretation = "HIGH", Recommendation = "Immediate safety assessment; initiate safety plan; mental health referral; consider higher level of care" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "Have you wished you were dead or wished you could go to sleep and not wake up?", MinValue = 0, MaxValue = 1, ResponseOptions = cssrsOptions },
                new() { ItemNumber = 2, QuestionText = "Have you actually had any thoughts of killing yourself?", MinValue = 0, MaxValue = 1, ResponseOptions = cssrsOptions },
                new() { ItemNumber = 3, QuestionText = "Have you been thinking about how you might do this?", MinValue = 0, MaxValue = 1, ResponseOptions = cssrsOptions },
                new() { ItemNumber = 4, QuestionText = "Have you had these thoughts and had some intention of acting on them?", MinValue = 0, MaxValue = 1, ResponseOptions = cssrsOptions },
                new() { ItemNumber = 5, QuestionText = "Have you started to work out or worked out the details of how to kill yourself? Do you intend to carry out this plan?", MinValue = 0, MaxValue = 1, ResponseOptions = cssrsOptions },
                new() { ItemNumber = 6, QuestionText = "Have you ever done anything, started to do anything, or prepared to do anything to end your life?", MinValue = 0, MaxValue = 1, ResponseOptions = cssrsOptions }
            }
        });

        // 6. AUDIT — Full Alcohol Use Disorders Identification Test (10 items)
        List<string> auditFreqOptions = new() { "Never (0)", "Monthly or less (1)", "2-4 times a month (2)", "2-3 times a week (3)", "4 or more times a week (4)" };
        List<string> auditQtyOptions = new() { "1 or 2 (0)", "3 or 4 (1)", "5 or 6 (2)", "7 to 9 (3)", "10 or more (4)" };
        List<string> auditBingeOptions = new() { "Never (0)", "Less than monthly (1)", "Monthly (2)", "Weekly (3)", "Daily or almost daily (4)" };
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "AUDIT",
            Description = "Alcohol Use Disorders Identification Test: a 10-item WHO screening for hazardous/harmful drinking",
            ItemCount = 10,
            MaxScore = 40,
            PositiveScreenThreshold = 8,
            ClinicalDomain = "SUBSTANCE_USE",
            Frequency = "ANNUAL",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 7, Interpretation = "LOW RISK", Recommendation = "Alcohol education" },
                new() { MinScore = 8, MaxScore = 15, Interpretation = "HAZARDOUS", Recommendation = "Simple advice / brief intervention" },
                new() { MinScore = 16, MaxScore = 19, Interpretation = "HARMFUL", Recommendation = "Brief counseling and continued monitoring" },
                new() { MinScore = 20, MaxScore = 40, Interpretation = "PROBABLE DEPENDENCE", Recommendation = "Refer to specialist for diagnostic evaluation and treatment" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "How often do you have a drink containing alcohol?", MinValue = 0, MaxValue = 4, ResponseOptions = auditFreqOptions },
                new() { ItemNumber = 2, QuestionText = "How many drinks containing alcohol do you have on a typical day when you are drinking?", MinValue = 0, MaxValue = 4, ResponseOptions = auditQtyOptions },
                new() { ItemNumber = 3, QuestionText = "How often do you have 6 or more drinks on one occasion?", MinValue = 0, MaxValue = 4, ResponseOptions = auditBingeOptions },
                new() { ItemNumber = 4, QuestionText = "How often during the last year have you found that you were not able to stop drinking once you had started?", MinValue = 0, MaxValue = 4, ResponseOptions = auditBingeOptions },
                new() { ItemNumber = 5, QuestionText = "How often during the last year have you failed to do what was normally expected of you because of drinking?", MinValue = 0, MaxValue = 4, ResponseOptions = auditBingeOptions },
                new() { ItemNumber = 6, QuestionText = "How often during the last year have you needed a first drink in the morning to get yourself going after a heavy drinking session?", MinValue = 0, MaxValue = 4, ResponseOptions = auditBingeOptions },
                new() { ItemNumber = 7, QuestionText = "How often during the last year have you had a feeling of guilt or remorse after drinking?", MinValue = 0, MaxValue = 4, ResponseOptions = auditBingeOptions },
                new() { ItemNumber = 8, QuestionText = "How often during the last year have you been unable to remember what happened the night before because of your drinking?", MinValue = 0, MaxValue = 4, ResponseOptions = auditBingeOptions },
                new() { ItemNumber = 9, QuestionText = "Have you or someone else been injured because of your drinking?", MinValue = 0, MaxValue = 4, ResponseOptions = new() { "No (0)", "Yes, but not in the last year (2)", "Yes, during the last year (4)" } },
                new() { ItemNumber = 10, QuestionText = "Has a relative, friend, doctor, or other health care worker been concerned about your drinking or suggested you cut down?", MinValue = 0, MaxValue = 4, ResponseOptions = new() { "No (0)", "Yes, but not in the last year (2)", "Yes, during the last year (4)" } }
            }
        });

        // 7. DAST-10 — Drug Abuse Screening Test
        List<string> dastOptions = new() { "No (0)", "Yes (1)" };
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "DAST-10",
            Description = "Drug Abuse Screening Test: a 10-item screening for drug use problems (excludes alcohol/tobacco)",
            ItemCount = 10,
            MaxScore = 10,
            PositiveScreenThreshold = 3,
            ClinicalDomain = "SUBSTANCE_USE",
            Frequency = "ANNUAL",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 0, Interpretation = "NO PROBLEMS", Recommendation = "No intervention needed" },
                new() { MinScore = 1, MaxScore = 2, Interpretation = "LOW LEVEL", Recommendation = "Monitor; brief intervention" },
                new() { MinScore = 3, MaxScore = 5, Interpretation = "MODERATE", Recommendation = "Brief intervention; consider outpatient treatment" },
                new() { MinScore = 6, MaxScore = 8, Interpretation = "SUBSTANTIAL", Recommendation = "Intensive outpatient or inpatient treatment" },
                new() { MinScore = 9, MaxScore = 10, Interpretation = "SEVERE", Recommendation = "Intensive inpatient treatment; refer to specialist" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "Have you used drugs other than those required for medical reasons?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 2, QuestionText = "Do you abuse more than one drug at a time?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 3, QuestionText = "Are you unable to stop using drugs when you want to?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 4, QuestionText = "Have you ever had blackouts or flashbacks as a result of drug use?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 5, QuestionText = "Do you ever feel bad or guilty about your drug use?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 6, QuestionText = "Does your spouse (or parents) ever complain about your involvement with drugs?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 7, QuestionText = "Have you neglected your family because of your use of drugs?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 8, QuestionText = "Have you engaged in illegal activities in order to obtain drugs?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 9, QuestionText = "Have you ever experienced withdrawal symptoms (felt sick) when you stopped taking drugs?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 10, QuestionText = "Have you had medical problems as a result of your drug use?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions }
            }
        });

        // 8. CAGE — Alcohol Screening (Cut-down, Annoyed, Guilty, Eye-opener)
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "CAGE",
            Description = "CAGE Questionnaire: a 4-item screening for alcohol problems",
            ItemCount = 4,
            MaxScore = 4,
            PositiveScreenThreshold = 2,
            ClinicalDomain = "SUBSTANCE_USE",
            Frequency = "ANNUAL",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 1, Interpretation = "NEGATIVE", Recommendation = "Low likelihood of alcohol problem" },
                new() { MinScore = 2, MaxScore = 4, Interpretation = "POSITIVE", Recommendation = "High likelihood of alcohol problem; further assessment needed" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "Have you ever felt you should CUT DOWN on your drinking?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 2, QuestionText = "Have people ANNOYED you by criticizing your drinking?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 3, QuestionText = "Have you ever felt bad or GUILTY about your drinking?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions },
                new() { ItemNumber = 4, QuestionText = "Have you ever had a drink first thing in the morning to steady your nerves or get rid of a hangover (EYE-OPENER)?", MinValue = 0, MaxValue = 1, ResponseOptions = dastOptions }
            }
        });

        // 9. CIWA-Ar — Clinical Institute Withdrawal Assessment for Alcohol (Revised)
        _state.State.Instruments.Add(new MhInstrumentDefinition
        {
            InstrumentName = "CIWA-Ar",
            Description = "Clinical Institute Withdrawal Assessment for Alcohol (Revised): a 10-item scale for alcohol withdrawal severity",
            ItemCount = 10,
            MaxScore = 67,
            PositiveScreenThreshold = 10,
            ClinicalDomain = "SUBSTANCE_USE",
            Frequency = "AS_NEEDED",
            ScoreRanges = new List<MhScoreRange>
            {
                new() { MinScore = 0, MaxScore = 9, Interpretation = "MINIMAL WITHDRAWAL", Recommendation = "Observation; no medication required" },
                new() { MinScore = 10, MaxScore = 18, Interpretation = "MILD WITHDRAWAL", Recommendation = "Consider medication; monitor every 4 hours" },
                new() { MinScore = 19, MaxScore = 30, Interpretation = "MODERATE WITHDRAWAL", Recommendation = "Medicate per protocol; monitor every 2 hours" },
                new() { MinScore = 31, MaxScore = 67, Interpretation = "SEVERE WITHDRAWAL", Recommendation = "Intensive care; medicate aggressively; continuous monitoring; risk of seizures/DT" }
            },
            Items = new List<MhInstrumentItem>
            {
                new() { ItemNumber = 1, QuestionText = "Nausea and vomiting", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "None (0)", "Mild nausea (1)", "(2)", "(3)", "Intermittent nausea with dry heaves (4)", "(5)", "(6)", "Constant nausea, frequent dry heaves and vomiting (7)" } },
                new() { ItemNumber = 2, QuestionText = "Tremor (arms extended, fingers spread apart)", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "No tremor (0)", "Not visible, can be felt fingertip to fingertip (1)", "(2)", "(3)", "Moderate, given patient's arms extended (4)", "(5)", "(6)", "Severe (7)" } },
                new() { ItemNumber = 3, QuestionText = "Paroxysmal sweats", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "No sweats visible (0)", "Barely perceptible sweating, palms moist (1)", "(2)", "(3)", "Beads of sweat obvious on forehead (4)", "(5)", "(6)", "Drenching sweats (7)" } },
                new() { ItemNumber = 4, QuestionText = "Anxiety", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "No anxiety, at ease (0)", "Mildly anxious (1)", "(2)", "(3)", "Moderately anxious, or guarded (4)", "(5)", "(6)", "Equivalent to acute panic states (7)" } },
                new() { ItemNumber = 5, QuestionText = "Agitation", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "Normal activity (0)", "Somewhat more than normal activity (1)", "(2)", "(3)", "Moderately fidgety and restless (4)", "(5)", "(6)", "Paces back and forth during interview or constantly thrashes about (7)" } },
                new() { ItemNumber = 6, QuestionText = "Tactile disturbances", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "None (0)", "Very mild itching, pins and needles, burning, or numbness (1)", "Mild itching, pins and needles, burning, or numbness (2)", "Moderate itching, pins and needles, burning, or numbness (3)", "Moderately severe hallucinations (4)", "Severe hallucinations (5)", "Extremely severe hallucinations (6)", "Continuous hallucinations (7)" } },
                new() { ItemNumber = 7, QuestionText = "Auditory disturbances", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "Not present (0)", "Very mild harshness or ability to frighten (1)", "Mild harshness or ability to frighten (2)", "Moderate harshness or ability to frighten (3)", "Moderately severe hallucinations (4)", "Severe hallucinations (5)", "Extremely severe hallucinations (6)", "Continuous hallucinations (7)" } },
                new() { ItemNumber = 8, QuestionText = "Visual disturbances", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "Not present (0)", "Very mild sensitivity (1)", "Mild sensitivity (2)", "Moderate sensitivity (3)", "Moderately severe hallucinations (4)", "Severe hallucinations (5)", "Extremely severe hallucinations (6)", "Continuous hallucinations (7)" } },
                new() { ItemNumber = 9, QuestionText = "Headache, fullness in head", MinValue = 0, MaxValue = 7, ResponseOptions = new() { "Not present (0)", "Very mild (1)", "Mild (2)", "Moderate (3)", "Moderately severe (4)", "Severe (5)", "Very severe (6)", "Extremely severe (7)" } },
                new() { ItemNumber = 10, QuestionText = "Orientation and clouding of sensorium", MinValue = 0, MaxValue = 4, ResponseOptions = new() { "Oriented (0)", "Uncertain about date (1)", "Date uncertain by more than 2 days (2)", "Disoriented in date by more than 2 days (3)", "Disoriented in place and/or person (4)" } }
            }
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
