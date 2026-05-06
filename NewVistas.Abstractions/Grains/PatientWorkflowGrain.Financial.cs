// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Integrated Billing (IB) ─────────────────────────────────────────────

    private IIBillingActionIndexGrain ActionIndex()
        => GrainFactory.GetGrain<IIBillingActionIndexGrain>($"IB-ACTION-IDX:{PatientId}");

    private IIBillingPatientGrain CopayAccount()
        => GrainFactory.GetGrain<IIBillingPatientGrain>($"IB-PATIENT:{PatientId}");

    private IMeansTestBillingClockGrain BillingClock()
        => GrainFactory.GetGrain<IMeansTestBillingClockGrain>($"IB-CLOCK:{PatientId}");

    private IPersonalPolicyIndexGrain PolicyIndex()
        => GrainFactory.GetGrain<IPersonalPolicyIndexGrain>($"IB-POLICY-IDX:{PatientId}");

    public Task<List<IBillingActionIndexEntry>> GetBillingActionsAsync()
        => ActionIndex().GetAllAsync();

    public Task<List<IBillingActionIndexEntry>> GetBillingActionsByStatusAsync(IBillingActionStatus status)
        => ActionIndex().GetByStatusAsync(status);

    public Task<IBillingActionState> GetBillingActionAsync(string billingActionId)
        => GrainFactory.GetGrain<IIBillingActionGrain>($"IB-ACTION:{billingActionId}").GetAsync();

    public async Task<string> RecordBillingActionAsync(
        string actionTypeCode,
        string actionTypeDescription,
        IBActionCategory actionCategory,
        decimal? chargeAmount,
        DateTime serviceDate,
        string enteredByUserId,
        string enteredByUserName,
        string? encounterId,
        string? diagnosisCode,
        string? procedureCode,
        string? locationId,
        string? orderId,
        string? prescriptionId,
        string? notes)
    {
        string actionId = Guid.NewGuid().ToString();
        IIBillingActionGrain actionGrain = GrainFactory.GetGrain<IIBillingActionGrain>($"IB-ACTION:{actionId}");

        await actionGrain.CreateAsync(
            PatientId, actionTypeCode, actionTypeDescription, actionCategory,
            chargeAmount, serviceDate, enteredByUserId, enteredByUserName,
            encounterId, diagnosisCode, procedureCode, locationId, orderId, prescriptionId, notes);

        // Update index
        await ActionIndex().AddOrUpdateAsync(new IBillingActionIndexEntry
        {
            BillingActionId       = actionId,
            PatientId             = PatientId,
            ActionTypeCode        = actionTypeCode,
            ActionTypeDescription = actionTypeDescription,
            Status                = IBillingActionStatus.Incomplete,
            ChargeAmount          = chargeAmount,
            ServiceDate           = serviceDate,
            EnteredDate           = DateTime.UtcNow,
        });

        // Post to copay account (initializes if needed)
        IIBillingPatientGrain acct = CopayAccount();
        await acct.EnsureInitializedAsync(PatientId);
        if (chargeAmount.HasValue && chargeAmount.Value > 0)
        {
            await acct.AddCopayTransactionAsync(
                actionId, actionTypeDescription, chargeAmount.Value, serviceDate, isExempt: false);
        }

        return actionId;
    }

    public async Task CancelBillingActionAsync(
        string billingActionId,
        string removeReasonCode,
        string removeReasonDescription,
        string removedByUserId)
    {
        IIBillingActionGrain actionGrain = GrainFactory.GetGrain<IIBillingActionGrain>($"IB-ACTION:{billingActionId}");
        await actionGrain.CancelAsync(removeReasonCode, removeReasonDescription, removedByUserId);

        IBillingActionState updated = await actionGrain.GetAsync();
        await ActionIndex().AddOrUpdateAsync(new IBillingActionIndexEntry
        {
            BillingActionId       = billingActionId,
            PatientId             = PatientId,
            ActionTypeCode        = updated.ActionTypeCode,
            ActionTypeDescription = updated.ActionTypeDescription,
            Status                = IBillingActionStatus.Cancelled,
            ChargeAmount          = updated.ChargeAmount,
            ServiceDate           = updated.ServiceDate ?? updated.DateEntered,
            EnteredDate           = updated.DateEntered,
        });
    }

    public async Task<IBillingPatientState> GetPatientCopayAccountAsync()
    {
        IIBillingPatientGrain acct = CopayAccount();
        await acct.EnsureInitializedAsync(PatientId);
        return await acct.GetAsync();
    }

    public Task SetCopayExemptionAsync(
        bool isExempt,
        string? reasonCode,
        DateTime? effectiveDate,
        DateTime? expirationDate)
        => CopayAccount().SetCopayExemptionAsync(isExempt, reasonCode, effectiveDate, expirationDate);

    public Task<MeansTestBillingClockState> GetBillingClockAsync()
        => BillingClock().GetAsync();

    public Task SetBillingClockAsync(
        string clockStatus,
        DateTime? startDate,
        DateTime? expirationDate,
        string? meansTestId,
        string? billingCategory,
        string? priorityGroup)
        => BillingClock().SetClockAsync(clockStatus, startDate, expirationDate, meansTestId, billingCategory, priorityGroup);

    // ─── Insurance ───────────────────────────────────────────────────────────

    public Task<List<PersonalPolicyIndexEntry>> GetPersonalPoliciesAsync()
        => PolicyIndex().GetAllAsync();

    public Task<PersonalPolicyState> GetPersonalPolicyAsync(string policyId)
        => GrainFactory.GetGrain<IPersonalPolicyGrain>($"IB-POLICY:{policyId}").GetAsync();

    public async Task<string> AddPersonalPolicyAsync(
        string? groupPlanId,
        string groupPlanName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? effectiveDate,
        DateTime? expirationDate,
        string? coverageType,
        bool isPrimary,
        decimal? copayAmount,
        string? pharmacyMemberId,
        string? notes)
    {
        string policyId = Guid.NewGuid().ToString();
        IPersonalPolicyGrain policyGrain = GrainFactory.GetGrain<IPersonalPolicyGrain>($"IB-POLICY:{policyId}");

        await policyGrain.CreateAsync(
            PatientId, groupPlanId, groupPlanName, subscriberId, subscriberName,
            relationshipToSubscriber, effectiveDate, expirationDate, coverageType,
            isPrimary, copayAmount, pharmacyMemberId, notes);

        // Fetch plan type from plan index entry if groupPlanId is supplied
        string? planType = null;
        if (!string.IsNullOrEmpty(groupPlanId))
        {
            IInsurancePlanGrain planGrain = GrainFactory.GetGrain<IInsurancePlanGrain>($"IB-PLAN:{groupPlanId}");
            InsurancePlanState plan = await planGrain.GetAsync();
            planType = plan.PlanType;
        }

        await PolicyIndex().AddOrUpdateAsync(new PersonalPolicyIndexEntry
        {
            PolicyId      = policyId,
            GroupPlanId   = groupPlanId,
            GroupPlanName = groupPlanName,
            PlanType      = planType,
            SubscriberId  = subscriberId,
            IsPrimary     = isPrimary,
            IsActive      = true,
            EffectiveDate = effectiveDate,
        });

        return policyId;
    }

    public async Task DeactivatePersonalPolicyAsync(string policyId)
    {
        IPersonalPolicyGrain policyGrain = GrainFactory.GetGrain<IPersonalPolicyGrain>($"IB-POLICY:{policyId}");
        await policyGrain.DeactivateAsync();

        PersonalPolicyState policy = await policyGrain.GetAsync();
        await PolicyIndex().AddOrUpdateAsync(new PersonalPolicyIndexEntry
        {
            PolicyId      = policyId,
            GroupPlanId   = policy.GroupPlanId,
            GroupPlanName = policy.GroupPlanName,
            SubscriberId  = policy.SubscriberId,
            IsPrimary     = policy.IsPrimary,
            IsActive      = false,
            EffectiveDate = policy.EffectiveDate,
        });
    }

    // ─── Registration — grain helpers ────────────────────────────────────────

    private IPatientEnrollmentGrain Enrollment()
        => GrainFactory.GetGrain<IPatientEnrollmentGrain>($"ENROLLMENT:{PatientId}");

    private IPrfAssignmentGrain PrfFlags()
        => GrainFactory.GetGrain<IPrfAssignmentGrain>($"PRF-ASSIGN:{PatientId}");

    private IMstHistoryGrain MstHistory()
        => GrainFactory.GetGrain<IMstHistoryGrain>($"MST:{PatientId}");

    private IPatientRelationGrain Relations()
        => GrainFactory.GetGrain<IPatientRelationGrain>($"PATIENT-RELATION:{PatientId}");

    private IIncomeHouseholdGrain Income()
        => GrainFactory.GetGrain<IIncomeHouseholdGrain>($"INCOME-HOUSEHOLD:{PatientId}");

    private ITreatingFacilityListGrain TreatingFacilities()
        => GrainFactory.GetGrain<ITreatingFacilityListGrain>($"TREATING-FAC:{PatientId}");

    // ─── Registration — Enrollment ────────────────────────────────────────────

    public async Task<PatientEnrollmentState> GetEnrollmentAsync()
    {
        await Enrollment().InitializeAsync(PatientId, null, null, null);
        return await Enrollment().GetAsync();
    }

    public Task SetEnrollmentStatusAsync(EnrollmentStatus status, string changedByUserId, string? notes)
        => Enrollment().UpdateStatusAsync(status, changedByUserId, notes);

    public Task SetEnrollmentPriorityGroupAsync(
        string priorityGroup,
        string? prioritySubgroup,
        bool meansTestRequired,
        bool copayExempt,
        string? copayExemptionReason)
        => Enrollment().SetPriorityGroupAsync(priorityGroup, prioritySubgroup, meansTestRequired, copayExempt, copayExemptionReason);

    // ─── Registration — PRF Flags ─────────────────────────────────────────────

    public Task<PrfAssignmentState> GetPrfFlagsAsync()
        => PrfFlags().GetAsync();

    public Task AssignPrfFlagAsync(
        string flagId,
        string flagName,
        string flagType,
        bool isNational,
        string assignedByUserId,
        string assignedByUserName,
        string? narrative)
        => PrfFlags().AssignFlagAsync(flagId, flagName, flagType, isNational, assignedByUserId, assignedByUserName, narrative);

    public Task DeactivatePrfFlagAsync(string flagId, string deactivatedReason, string deactivatedByUserId)
        => PrfFlags().DeactivateFlagAsync(flagId, deactivatedReason, deactivatedByUserId);

    // ─── Registration — MST History ───────────────────────────────────────────

    public Task<MstHistoryState> GetMstHistoryAsync()
        => MstHistory().GetAsync();

    public Task RecordMstScreeningAsync(
        DateTime screeningDate,
        MstStatus status,
        string screenedByUserId,
        string screenedByUserName,
        string? location,
        string? notes)
        => MstHistory().RecordScreeningAsync(screeningDate, status, screenedByUserId, screenedByUserName, location, notes);

    // ─── Registration — Patient Relations ────────────────────────────────────

    public Task<PatientRelationState> GetPatientRelationsAsync()
        => Relations().GetAsync();

    public Task<string> AddOrUpdatePatientRelationAsync(PatientRelation relation)
        => Relations().AddOrUpdateRelationAsync(relation);

    public Task RemovePatientRelationAsync(string relationId)
        => Relations().RemoveRelationAsync(relationId);

    // ─── Registration — Income / Household ───────────────────────────────────

    public Task<IncomeHouseholdState> GetIncomeHouseholdAsync()
        => Income().GetAsync();

    public Task<string> AddOrUpdateIncomePersonAsync(IncomePerson member)
        => Income().AddOrUpdateMemberAsync(member);

    public Task RecordMeansTestDecisionAsync(string decision, DateTime decisionDate, decimal? threshold)
        => Income().RecordMeansTestDecisionAsync(decision, decisionDate, threshold);

    // ─── Registration — Treating Facilities ──────────────────────────────────

    public Task<TreatingFacilityListState> GetTreatingFacilitiesAsync()
        => TreatingFacilities().GetAsync();

    public Task AddOrUpdateTreatingFacilityAsync(TreatingFacilityEntry facility)
        => TreatingFacilities().AddOrUpdateFacilityAsync(facility);

    public Task SetPrimaryTreatingFacilityAsync(string facilityId, string facilityName)
        => TreatingFacilities().SetPrimaryFacilityAsync(facilityId, facilityName);

    // ─── AR private helpers ───────────────────────────────────────────────────

    private IARDebtorGrain ARDebtor()
        => GrainFactory.GetGrain<IARDebtorGrain>($"AR-DEBTOR:{PatientId}");

    private IARAccountIndexGrain ARAccountIndex()
        => GrainFactory.GetGrain<IARAccountIndexGrain>($"AR-ACCT-IDX:{PatientId}");

    // ─── Accounts Receivable ──────────────────────────────────────────────────

    public Task<ARDebtorState> GetARDebtorAsync()
        => ARDebtor().GetAsync();

    public Task<List<ARAccountIndexEntry>> GetARAccountsAsync()
        => ARAccountIndex().GetAllAsync();

    public Task<List<ARAccountIndexEntry>> GetActiveARAccountsAsync()
        => ARAccountIndex().GetActiveAsync();

    public async Task<ARAccountState> GetARAccountAsync(string arAccountId)
    {
        IARAccountGrain acct = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}");
        return await acct.GetAsync();
    }

    public async Task<string> CreateARAccountAsync(
        string? billingActionId,
        string arCategory,
        decimal originalAmount,
        DateTime? dueDate)
    {
        string arAccountId = Guid.NewGuid().ToString();
        ARAccountCategory category = Enum.Parse<ARAccountCategory>(arCategory, ignoreCase: true);

        IARAccountGrain acct = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}");
        await acct.CreateAsync(PatientId, billingActionId, category, originalAmount, dueDate);

        ARAccountState state = await acct.GetAsync();
        await ARAccountIndex().AddOrUpdateAsync(new ARAccountIndexEntry
        {
            ARAccountId    = arAccountId,
            PatientId      = PatientId,
            ARCategory     = state.ARCategory.ToString(),
            ARStatus       = state.ARStatus.ToString(),
            OriginalAmount = state.OriginalAmount,
            CurrentBalance = state.CurrentBalance,
            DateEstablished = state.DateEstablished,
        });

        await ARDebtor().EnsureInitializedAsync(PatientId, PatientId);
        List<ARAccountIndexEntry> all = await ARAccountIndex().GetAllAsync();
        decimal totalOwed    = all.Sum(e => e.OriginalAmount);
        decimal totalBalance = all.Sum(e => e.CurrentBalance);
        decimal totalPaid    = totalOwed - totalBalance;
        await ARDebtor().UpdateBalanceSummaryAsync(totalOwed, totalPaid, totalBalance);

        return arAccountId;
    }

    public async Task<string> PostARPaymentAsync(
        string arAccountId,
        decimal amount,
        string paymentMethod,
        string appliedByUserId,
        string appliedByUserName,
        string? receiptNumber,
        string? checkNumber,
        string? notes)
    {
        IARAccountGrain acct = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}");
        string txnId = await acct.PostPaymentAsync(
            amount, paymentMethod, appliedByUserId, appliedByUserName,
            receiptNumber, checkNumber, notes);

        ARAccountState state = await acct.GetAsync();
        await ARAccountIndex().AddOrUpdateAsync(new ARAccountIndexEntry
        {
            ARAccountId     = arAccountId,
            PatientId       = PatientId,
            ARCategory      = state.ARCategory.ToString(),
            ARStatus        = state.ARStatus.ToString(),
            OriginalAmount  = state.OriginalAmount,
            CurrentBalance  = state.CurrentBalance,
            DateEstablished = state.DateEstablished,
        });

        List<ARAccountIndexEntry> all = await ARAccountIndex().GetAllAsync();
        decimal totalOwed    = all.Sum(e => e.OriginalAmount);
        decimal totalBalance = all.Sum(e => e.CurrentBalance);
        decimal totalPaid    = totalOwed - totalBalance;
        await ARDebtor().UpdateBalanceSummaryAsync(totalOwed, totalPaid, totalBalance);

        return txnId;
    }

    public async Task<string> PostARAdjustmentAsync(
        string arAccountId,
        decimal amount,
        string adjustmentType,
        string appliedByUserId,
        string appliedByUserName,
        string? notes)
    {
        IARAccountGrain acct = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}");
        string txnId = await acct.PostAdjustmentAsync(
            amount, adjustmentType, appliedByUserId, appliedByUserName, notes);

        ARAccountState state = await acct.GetAsync();
        await ARAccountIndex().AddOrUpdateAsync(new ARAccountIndexEntry
        {
            ARAccountId     = arAccountId,
            PatientId       = PatientId,
            ARCategory      = state.ARCategory.ToString(),
            ARStatus        = state.ARStatus.ToString(),
            OriginalAmount  = state.OriginalAmount,
            CurrentBalance  = state.CurrentBalance,
            DateEstablished = state.DateEstablished,
        });

        List<ARAccountIndexEntry> all = await ARAccountIndex().GetAllAsync();
        decimal totalOwed    = all.Sum(e => e.OriginalAmount);
        decimal totalBalance = all.Sum(e => e.CurrentBalance);
        decimal totalPaid    = totalOwed - totalBalance;
        await ARDebtor().UpdateBalanceSummaryAsync(totalOwed, totalPaid, totalBalance);

        return txnId;
    }

    public async Task<string> WaiveARAccountAsync(
        string arAccountId,
        decimal waivedAmount,
        string waivedByUserId,
        string waivedByUserName,
        string reason)
    {
        IARAccountGrain acct = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}");
        string txnId = await acct.WaiveAsync(waivedAmount, waivedByUserId, waivedByUserName, reason);

        ARAccountState state = await acct.GetAsync();
        await ARAccountIndex().AddOrUpdateAsync(new ARAccountIndexEntry
        {
            ARAccountId     = arAccountId,
            PatientId       = PatientId,
            ARCategory      = state.ARCategory.ToString(),
            ARStatus        = state.ARStatus.ToString(),
            OriginalAmount  = state.OriginalAmount,
            CurrentBalance  = state.CurrentBalance,
            DateEstablished = state.DateEstablished,
        });

        List<ARAccountIndexEntry> all = await ARAccountIndex().GetAllAsync();
        decimal totalOwed    = all.Sum(e => e.OriginalAmount);
        decimal totalBalance = all.Sum(e => e.CurrentBalance);
        decimal totalPaid    = totalOwed - totalBalance;
        await ARDebtor().UpdateBalanceSummaryAsync(totalOwed, totalPaid, totalBalance);

        return txnId;
    }

    public Task AccrueARInterestAsync(string arAccountId, decimal interestAmount, string appliedByUserId)
        => GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}").AccrueInterestAsync(interestAmount, appliedByUserId);

    public Task AccrueARPenaltyAsync(string arAccountId, decimal penaltyAmount, string appliedByUserId)
        => GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}").AccruePenaltyAsync(penaltyAmount, appliedByUserId);

    public Task AccrueARAdminCostAsync(string arAccountId, decimal adminCostAmount, string appliedByUserId)
        => GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{arAccountId}").AccrueAdminCostAsync(adminCostAmount, appliedByUserId);

    // ─── Fee Basis helpers ────────────────────────────────────────────────────────

    private IFeePatientGrain FeePatient()
        => GrainFactory.GetGrain<IFeePatientGrain>($"FEE-PATIENT:{PatientId}");

    private IFeeAuthorizationIndexGrain FeeAuthIndex()
        => GrainFactory.GetGrain<IFeeAuthorizationIndexGrain>($"FEE-AUTH-IDX:{PatientId}");

    private IFeeInvoiceIndexGrain FeeInvoiceIndex()
        => GrainFactory.GetGrain<IFeeInvoiceIndexGrain>($"FEE-INVOICE-IDX:{PatientId}");

    // ─── Fee Basis workflow methods ───────────────────────────────────────────────

    public async Task<GrainStates.FeePatientState> GetFeePatientAsync()
    {
        await FeePatient().EnsureInitializedAsync(PatientId);
        return await FeePatient().GetAsync();
    }

    public Task<List<GrainStates.FeeAuthorizationIndexEntry>> GetFeeAuthorizationsAsync()
        => FeeAuthIndex().GetAllAsync();

    public async Task<GrainStates.FeeAuthorizationState> GetFeeAuthorizationAsync(string authId)
    {
        IFeeAuthorizationGrain auth = GrainFactory.GetGrain<IFeeAuthorizationGrain>($"FEE-AUTH:{authId}");
        return await auth.GetAsync();
    }

    public async Task<string> CreateFeeAuthorizationAsync(
        string vendorId,
        string vendorName,
        string serviceType,
        DateTime authorizationDate,
        DateTime effectiveDate,
        DateTime? expirationDate,
        decimal authorizedAmount,
        string authorizedByUserId,
        string authorizedByUserName,
        string serviceDescription,
        int? maxVisits,
        string? diagnosisCode,
        string? authorizationNumber,
        string? notes)
    {
        string authId = $"FEE-AUTH:{Guid.NewGuid()}";
        GrainStates.FeeServiceType svcType = Enum.TryParse<GrainStates.FeeServiceType>(serviceType, out GrainStates.FeeServiceType st)
            ? st
            : GrainStates.FeeServiceType.Other;

        IFeeAuthorizationGrain auth = GrainFactory.GetGrain<IFeeAuthorizationGrain>(authId);
        await auth.CreateAsync(
            PatientId, vendorId, vendorName, svcType,
            authorizationDate, effectiveDate, expirationDate, authorizedAmount,
            authorizedByUserId, authorizedByUserName, serviceDescription,
            maxVisits, diagnosisCode, authorizationNumber, notes);

        await FeeAuthIndex().AddOrUpdateAsync(new GrainStates.FeeAuthorizationIndexEntry
        {
            AuthorizationId   = authId,
            PatientId         = PatientId,
            VendorName        = vendorName,
            ServiceType       = serviceType,
            Status            = GrainStates.FeeAuthorizationStatus.Active.ToString(),
            AuthorizedAmount  = authorizedAmount,
            SpentAmount       = 0m,
            AuthorizationDate = authorizationDate,
        });

        await FeePatient().EnsureInitializedAsync(PatientId);
        List<GrainStates.FeeAuthorizationIndexEntry> allAuths = await FeeAuthIndex().GetAllAsync();
        decimal totalAuthorized = allAuths.Sum(e => e.AuthorizedAmount);
        decimal totalPaid       = allAuths.Sum(e => e.SpentAmount);
        int     activeCount     = allAuths.Count(e => e.Status == "Active" || e.Status == "Pending");
        await FeePatient().UpdateSummaryAsync(totalAuthorized, totalPaid, activeCount);

        return authId;
    }

    public Task<List<GrainStates.FeeInvoiceIndexEntry>> GetFeeInvoicesAsync()
        => FeeInvoiceIndex().GetAllAsync();

    // ─── Agent Cashier helper + methods ──────────────────────────────────────────

    private ICashierReceiptIndexGrain CashierReceiptIndex()
        => GrainFactory.GetGrain<ICashierReceiptIndexGrain>($"CASHIER-RECEIPT-IDX:{PatientId}");

    public Task<List<GrainStates.CashierReceiptIndexEntry>> GetCashierReceiptsAsync()
        => CashierReceiptIndex().GetAllAsync();

    // ─── EDI / Electronic Billing helper + methods ───────────────────────────────

    private IEdiClaimIndexGrain EdiClaimIndex()
        => GrainFactory.GetGrain<IEdiClaimIndexGrain>($"EDI-CLAIM-IDX:{PatientId}");

    public Task<List<GrainStates.EdiClaimIndexEntry>> GetEdiClaimsAsync()
        => EdiClaimIndex().GetAllAsync();

    // ─── Insurance Eligibility Verification (EDI 270/271) — IBCNEDE*.m ─────

    private IEligibilityVerificationIndexGrain EligibilityIndex()
        => GrainFactory.GetGrain<IEligibilityVerificationIndexGrain>($"ELIG-IDX:{PatientId}");

    private IPayerConfigIndexGrain PayerConfigIndex()
        => GrainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX");

    public async Task<string> SubmitEligibilityInquiryAsync(
        string? insurancePlanId,
        string? personalPolicyId,
        string payerId,
        string payerName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? patientDateOfBirth,
        List<string> serviceTypeCodes,
        DateTime serviceDate,
        string? initiatedByUserId,
        string? initiatedByUserName,
        string? notes)
    {
        string inquiryId = Guid.NewGuid().ToString();
        IEligibilityInquiryGrain inquiry = GrainFactory.GetGrain<IEligibilityInquiryGrain>($"ELIG-270:{inquiryId}");

        await inquiry.CreateAsync(
            PatientId, insurancePlanId, personalPolicyId,
            payerId, payerName, subscriberId, subscriberName,
            relationshipToSubscriber, patientDateOfBirth,
            serviceTypeCodes, serviceDate,
            initiatedByUserId, initiatedByUserName, notes);

        // Submit the inquiry (simulate sending 270 to payer)
        string traceNumber = $"TN-{DateTime.UtcNow:yyyyMMddHHmmss}-{inquiryId[..8]}";
        await inquiry.SubmitAsync(traceNumber);

        // Simulate payer 271 response — eligible with coverage details
        List<GrainStates.EligibilityBenefitDetail> benefits = new()
        {
            new() { BenefitType = "DEDUCTIBLE", ServiceTypeCode = "30", ServiceTypeDescription = "Health Benefit Plan Coverage", TimePeriod = "CALENDAR_YEAR", Amount = 500m, NetworkIndicator = "IN" },
            new() { BenefitType = "COPAY", ServiceTypeCode = "1", ServiceTypeDescription = "Medical Care", TimePeriod = "VISIT", Amount = 30m, NetworkIndicator = "IN" },
            new() { BenefitType = "COINSURANCE", ServiceTypeCode = "1", ServiceTypeDescription = "Medical Care", TimePeriod = "CALENDAR_YEAR", Percentage = 0.20m, NetworkIndicator = "IN" },
            new() { BenefitType = "OUT_OF_POCKET", ServiceTypeCode = "30", ServiceTypeDescription = "Health Benefit Plan Coverage", TimePeriod = "CALENDAR_YEAR", Amount = 6000m, NetworkIndicator = "IN" },
        };

        await inquiry.RecordEligibleResponseAsync(
            GrainStates.CoverageLevel.ActiveCoverage,
            $"{payerName} Standard Plan",
            DateTime.UtcNow.AddMonths(-6),
            DateTime.UtcNow.AddMonths(6),
            "GRP-" + payerId[..Math.Min(8, payerId.Length)],
            benefits,
            "Member is eligible for benefits as of the date of service.");

        // Update the per-patient eligibility verification index
        GrainStates.EligibilityInquiryState finalState = await inquiry.GetAsync();
        await EligibilityIndex().AddOrUpdateAsync(new GrainStates.EligibilityVerificationIndexEntry
        {
            InquiryId     = inquiryId,
            PayerName     = payerName,
            SubscriberId  = subscriberId,
            ServiceDate   = serviceDate,
            Status        = finalState.Status,
            CoverageLevel = finalState.CoverageLevel,
            SubmittedDate = finalState.SubmittedDate,
            ResponseDate  = finalState.ResponseDate,
        });

        // Update verification date on the insurance plan if linked
        if (!string.IsNullOrEmpty(insurancePlanId))
        {
            IInsurancePlanGrain plan = GrainFactory.GetGrain<IInsurancePlanGrain>($"IB-PLAN:{insurancePlanId}");
            await plan.VerifyAsync("ELECTRONIC-270/271", DateTime.UtcNow);
        }

        return inquiryId;
    }

    public Task<GrainStates.EligibilityInquiryState> GetEligibilityInquiryAsync(string inquiryId)
        => GrainFactory.GetGrain<IEligibilityInquiryGrain>($"ELIG-270:{inquiryId}").GetAsync();

    public Task<List<GrainStates.EligibilityVerificationIndexEntry>> GetEligibilityVerificationHistoryAsync()
        => EligibilityIndex().GetAllAsync();

    public Task<List<GrainStates.EligibilityVerificationIndexEntry>> GetEligibleVerificationsAsync()
        => EligibilityIndex().GetEligibleAsync();

    public Task<GrainStates.EligibilityVerificationIndexEntry?> GetLatestVerificationForPayerAsync(string payerName)
        => EligibilityIndex().GetLatestForPayerAsync(payerName);

    public Task<List<GrainStates.PayerConfigIndexEntry>> GetPayerConfigListAsync()
        => PayerConfigIndex().GetAllAsync();

    public Task<List<GrainStates.PayerConfigIndexEntry>> SearchPayerConfigsAsync(string query)
        => PayerConfigIndex().SearchAsync(query);

    public Task<List<GrainStates.PayerConfigIndexEntry>> GetRealTimePayersAsync()
        => PayerConfigIndex().GetRealTimePayersAsync();

    // ─── Collection Letters (PRCA) — RCCLLT*.m, RCCL*.m ────────────────────

    private ICollectionLetterIndexGrain LetterIndex()
        => GrainFactory.GetGrain<ICollectionLetterIndexGrain>($"AR-LETTER-IDX:{PatientId}");

    public async Task<string> GenerateCollectionLetterAsync(
        GrainStates.CollectionLetterType letterType,
        string? generatedByUserId,
        string? generatedByUserName,
        string? notes)
    {
        // Get the patient's active AR accounts for line items
        List<GrainStates.ARAccountIndexEntry> activeAccounts = await ARAccountIndex().GetActiveAsync();

        // Build line items from active accounts
        DateTime now = DateTime.UtcNow;
        List<GrainStates.CollectionLetterLineItem> lineItems = new();
        decimal totalDue = 0m;

        foreach (GrainStates.ARAccountIndexEntry acct in activeAccounts)
        {
            if (acct.CurrentBalance <= 0) continue;
            int daysPastDue = (int)(now - acct.DateEstablished).TotalDays;
            lineItems.Add(new GrainStates.CollectionLetterLineItem
            {
                ARAccountId     = acct.ARAccountId,
                Description     = $"{acct.ARCategory} — {acct.DateEstablished:yyyy-MM-dd}",
                OriginalAmount  = acct.OriginalAmount,
                CurrentBalance  = acct.CurrentBalance,
                DateEstablished = acct.DateEstablished,
                DaysPastDue     = daysPastDue,
            });
            totalDue += acct.CurrentBalance;
        }

        // Get patient name from debtor record
        GrainStates.ARDebtorState debtor = await ARDebtor().GetAsync();
        string patientName = !string.IsNullOrEmpty(debtor.Name) ? debtor.Name : PatientId;
        string? address = debtor.Address;

        // Get next dunning sequence
        int dunningSeq = await LetterIndex().GetNextDunningSequenceAsync();

        // Create the letter grain
        string letterId = Guid.NewGuid().ToString();
        ICollectionLetterGrain letter = GrainFactory.GetGrain<ICollectionLetterGrain>($"AR-LETTER:{letterId}");
        await letter.GenerateAsync(
            PatientId, patientName, address, letterType,
            lineItems, totalDue, null, 30, dunningSeq,
            generatedByUserId, generatedByUserName, notes);

        // Update the per-patient letter index
        await LetterIndex().AddOrUpdateAsync(new GrainStates.CollectionLetterIndexEntry
        {
            LetterId        = letterId,
            LetterType      = letterType,
            Status          = GrainStates.CollectionLetterStatus.Generated,
            TotalAmountDue  = totalDue,
            GeneratedDate   = now,
            DunningSequence = dunningSeq,
        });

        return letterId;
    }

    public Task<List<GrainStates.CollectionLetterIndexEntry>> GetCollectionLettersAsync()
        => LetterIndex().GetAllAsync();

    public Task<GrainStates.CollectionLetterState> GetCollectionLetterAsync(string letterId)
        => GrainFactory.GetGrain<ICollectionLetterGrain>($"AR-LETTER:{letterId}").GetAsync();

    public async Task MarkCollectionLetterPrintedAsync(string letterId)
    {
        ICollectionLetterGrain letter = GrainFactory.GetGrain<ICollectionLetterGrain>($"AR-LETTER:{letterId}");
        await letter.MarkPrintedAsync();
        GrainStates.CollectionLetterState state = await letter.GetAsync();
        await LetterIndex().AddOrUpdateAsync(new GrainStates.CollectionLetterIndexEntry
        {
            LetterId = letterId, LetterType = state.LetterType,
            Status = state.Status, TotalAmountDue = state.TotalAmountDue,
            GeneratedDate = state.GeneratedDate, DunningSequence = state.DunningSequence,
        });
    }

    public async Task MarkCollectionLetterMailedAsync(string letterId)
    {
        ICollectionLetterGrain letter = GrainFactory.GetGrain<ICollectionLetterGrain>($"AR-LETTER:{letterId}");
        await letter.MarkMailedAsync();
        GrainStates.CollectionLetterState state = await letter.GetAsync();
        await LetterIndex().AddOrUpdateAsync(new GrainStates.CollectionLetterIndexEntry
        {
            LetterId = letterId, LetterType = state.LetterType,
            Status = state.Status, TotalAmountDue = state.TotalAmountDue,
            GeneratedDate = state.GeneratedDate, DunningSequence = state.DunningSequence,
        });
    }

    public async Task MarkCollectionLetterReturnedAsync(string letterId)
    {
        ICollectionLetterGrain letter = GrainFactory.GetGrain<ICollectionLetterGrain>($"AR-LETTER:{letterId}");
        await letter.MarkReturnedAsync();
        GrainStates.CollectionLetterState state = await letter.GetAsync();
        await LetterIndex().AddOrUpdateAsync(new GrainStates.CollectionLetterIndexEntry
        {
            LetterId = letterId, LetterType = state.LetterType,
            Status = state.Status, TotalAmountDue = state.TotalAmountDue,
            GeneratedDate = state.GeneratedDate, DunningSequence = state.DunningSequence,
        });
    }

    public async Task CancelCollectionLetterAsync(string letterId, string? reason)
    {
        ICollectionLetterGrain letter = GrainFactory.GetGrain<ICollectionLetterGrain>($"AR-LETTER:{letterId}");
        await letter.CancelAsync(reason);
        GrainStates.CollectionLetterState state = await letter.GetAsync();
        await LetterIndex().AddOrUpdateAsync(new GrainStates.CollectionLetterIndexEntry
        {
            LetterId = letterId, LetterType = state.LetterType,
            Status = state.Status, TotalAmountDue = state.TotalAmountDue,
            GeneratedDate = state.GeneratedDate, DunningSequence = state.DunningSequence,
        });
    }

    // ─── Financial Reporting / AR Aging (PRCA) — RCRP*.m ────────────────────

    private IARAgingReportGrain AgingReport()
        => GrainFactory.GetGrain<IARAgingReportGrain>($"AR-AGING:{PatientId}");

    public async Task<GrainStates.ARAgingReportState> GenerateARAgingReportAsync(
        string? generatedByUserId,
        string? generatedByUserName)
    {
        // Get all AR accounts for this patient
        List<GrainStates.ARAccountIndexEntry> allAccounts = await ARAccountIndex().GetAllAsync();
        DateTime now = DateTime.UtcNow;

        // Classify each account into aging buckets
        List<GrainStates.AgingAccountDetail> details = new();
        decimal totalBalance = 0m;
        decimal totalPaid = 0m;
        decimal totalOriginal = 0m;
        int delinquentCount = 0;
        decimal delinquentBalance = 0m;
        int collectionCount = 0;
        decimal collectionBalance = 0m;

        foreach (GrainStates.ARAccountIndexEntry acct in allAccounts)
        {
            int daysOut = (int)(now - acct.DateEstablished).TotalDays;
            GrainStates.AgingBucket bucket = daysOut switch
            {
                <= 30 => GrainStates.AgingBucket.Current,
                <= 60 => GrainStates.AgingBucket.ThirtyOneToSixty,
                <= 90 => GrainStates.AgingBucket.SixtyOneToNinety,
                <= 120 => GrainStates.AgingBucket.NinetyOneToOneTwenty,
                _ => GrainStates.AgingBucket.OverOneTwenty,
            };

            _ = Enum.TryParse<GrainStates.ARAccountCategory>(acct.ARCategory, out GrainStates.ARAccountCategory parsedCategory);
            _ = Enum.TryParse<GrainStates.ARAccountStatus>(acct.ARStatus, out GrainStates.ARAccountStatus parsedStatus);

            details.Add(new GrainStates.AgingAccountDetail
            {
                ARAccountId     = acct.ARAccountId,
                PatientId       = PatientId,
                Category        = parsedCategory,
                Status          = parsedStatus,
                OriginalAmount  = acct.OriginalAmount,
                CurrentBalance  = acct.CurrentBalance,
                DateEstablished = acct.DateEstablished,
                DaysOutstanding = daysOut,
                Bucket          = bucket,
            });

            totalBalance += acct.CurrentBalance;
            totalOriginal += acct.OriginalAmount;
            totalPaid += (acct.OriginalAmount - acct.CurrentBalance);

            if (acct.ARStatus == nameof(GrainStates.ARAccountStatus.InCollection))
            {
                collectionCount++;
                collectionBalance += acct.CurrentBalance;
            }
            else if (daysOut > 90 && acct.CurrentBalance > 0)
            {
                delinquentCount++;
                delinquentBalance += acct.CurrentBalance;
            }
        }

        decimal avgDays = details.Count > 0 ? (decimal)details.Average(d => d.DaysOutstanding) : 0m;
        decimal collectionRate = (totalPaid + totalBalance) > 0 ? totalPaid / (totalPaid + totalBalance) : 0m;

        GrainStates.RevenueCycleMetrics metrics = new()
        {
            TotalARBalance          = totalBalance,
            TotalActiveAccounts     = allAccounts.Count(a => a.ARStatus == nameof(GrainStates.ARAccountStatus.Active)),
            AverageDaysOutstanding  = Math.Round(avgDays, 1),
            CollectionRate          = Math.Round(collectionRate, 4),
            TotalPaymentsReceived   = totalPaid,
            TotalNewCharges         = totalOriginal,
            DelinquentAccountCount  = delinquentCount,
            DelinquentBalance       = delinquentBalance,
            InCollectionCount       = collectionCount,
            InCollectionBalance     = collectionBalance,
            TopReferralCount        = allAccounts.Count(a => a.ARStatus == nameof(GrainStates.ARAccountStatus.TreasuryOffset)),
            ReportDate              = now,
        };

        await AgingReport().GenerateAsync(PatientId, details, metrics, generatedByUserId, generatedByUserName);
        return await AgingReport().GetAsync();
    }

    public Task<GrainStates.ARAgingReportState> GetARAgingReportAsync()
        => AgingReport().GetAsync();

    public Task<List<GrainStates.AgingBucketSummary>> GetARAgingBucketsAsync()
        => AgingReport().GetBucketSummariesAsync();

    public Task<GrainStates.RevenueCycleMetrics?> GetRevenueCycleMetricsAsync()
        => AgingReport().GetMetricsAsync();

    public Task<List<GrainStates.AgingAccountDetail>> GetAccountsByAgingBucketAsync(GrainStates.AgingBucket bucket)
        => AgingReport().GetAccountsByBucketAsync(bucket);

    // ─── Claim Status Inquiry (EDI 276/277) — IBCSC*.m ─────────────────────

    private IClaimStatusInquiryIndexGrain CsiIndex()
        => GrainFactory.GetGrain<IClaimStatusInquiryIndexGrain>($"CSI-IDX:{PatientId}");

    public async Task<string> SubmitClaimStatusInquiryAsync(
        string claimId, string payerId, string payerName,
        string? initiatedByUserId, string? initiatedByUserName, string? notes)
    {
        // Look up claim billed amount from the EDI claim
        IEdiClaimGrain claim = GrainFactory.GetGrain<IEdiClaimGrain>($"EDI-CLAIM:{claimId}");
        GrainStates.EdiClaimState claimState = await claim.GetAsync();

        string inquiryId = Guid.NewGuid().ToString();
        IClaimStatusInquiryGrain inquiry = GrainFactory.GetGrain<IClaimStatusInquiryGrain>($"CSI-276:{inquiryId}");

        await inquiry.CreateAsync(PatientId, claimId, payerId, payerName,
            claimState.TotalBilledAmount, initiatedByUserId, initiatedByUserName, notes);

        string traceNumber = $"CSI-{DateTime.UtcNow:yyyyMMddHHmmss}-{inquiryId[..8]}";
        await inquiry.SubmitAsync(traceNumber);

        // Simulate 277 response based on claim status
        GrainStates.ClaimStatusCategory category = claimState.Status switch
        {
            GrainStates.EdiClaimStatus.Draft or GrainStates.EdiClaimStatus.InTransmission
                => GrainStates.ClaimStatusCategory.Received,
            GrainStates.EdiClaimStatus.Transmitted or GrainStates.EdiClaimStatus.Acknowledged
                => GrainStates.ClaimStatusCategory.Pending,
            GrainStates.EdiClaimStatus.Paid or GrainStates.EdiClaimStatus.PartiallyPaid
                => GrainStates.ClaimStatusCategory.Finalized,
            GrainStates.EdiClaimStatus.Rejected
                => GrainStates.ClaimStatusCategory.Rejected,
            GrainStates.EdiClaimStatus.Denied
                => GrainStates.ClaimStatusCategory.Denied,
            _ => GrainStates.ClaimStatusCategory.Pending,
        };

        List<GrainStates.ClaimStatusDetail> statusDetails = new()
        {
            new()
            {
                StatusCategoryCode = category.ToString(),
                StatusCategoryDescription = $"Claim is {category}",
                TotalChargeAmount = claimState.TotalBilledAmount,
                PaymentAmount = claimState.PaidAmount,
                EffectiveDate = claimState.PaymentDate ?? DateTime.UtcNow,
                Message = $"Claim {claimId} status as of {DateTime.UtcNow:yyyy-MM-dd}",
            },
        };

        await inquiry.RecordResponseAsync(category, statusDetails, claimState.PaidAmount,
            $"Status inquiry processed for claim {claimId}.");

        // Update index
        GrainStates.ClaimStatusInquiryState finalState = await inquiry.GetAsync();
        await CsiIndex().AddOrUpdateAsync(new GrainStates.ClaimStatusInquiryIndexEntry
        {
            InquiryId           = inquiryId,
            ClaimId             = claimId,
            PayerName           = payerName,
            Status              = finalState.Status,
            ClaimStatusCategory = finalState.ClaimStatusCategory,
            SubmittedDate       = finalState.SubmittedDate,
            ResponseDate        = finalState.ResponseDate,
        });

        return inquiryId;
    }

    public Task<GrainStates.ClaimStatusInquiryState> GetClaimStatusInquiryAsync(string inquiryId)
        => GrainFactory.GetGrain<IClaimStatusInquiryGrain>($"CSI-276:{inquiryId}").GetAsync();

    public Task<List<GrainStates.ClaimStatusInquiryIndexEntry>> GetClaimStatusInquiriesAsync()
        => CsiIndex().GetAllAsync();

    public Task<List<GrainStates.ClaimStatusInquiryIndexEntry>> GetClaimStatusInquiriesByClaimAsync(string claimId)
        => CsiIndex().GetByClaimAsync(claimId);

    // ─── Automatic Eligibility Determination — DGENELA.m ────────────────────

    private IAutoEligibilityDeterminationGrain EligibilityDetermination()
        => GrainFactory.GetGrain<IAutoEligibilityDeterminationGrain>($"ELIG-DET:{PatientId}");

    public async Task<GrainStates.AutoEligibilityDeterminationState> RunAutoEligibilityDeterminationAsync(
        string? determinedByUserId, string? determinedByUserName)
    {
        // Gather current enrollment state
        IPatientEnrollmentGrain enrollment = GrainFactory.GetGrain<IPatientEnrollmentGrain>($"ENROLLMENT:{PatientId}");
        GrainStates.PatientEnrollmentState enrollState = await enrollment.GetAsync();

        // Gather means test data
        IIBillingPatientGrain bp = CopayAccount();
        GrainStates.IBillingPatientState bpState = await bp.GetAsync();

        // Run the determination
        GrainStates.AutoEligibilityDeterminationState result = await EligibilityDetermination().DetermineAsync(
            PatientId,
            enrollState.EnrollmentStatus.ToString(),
            enrollState.PriorityGroup,
            enrollState.PrioritySubgroup,
            enrollState.MeansTestRequired,
            !string.IsNullOrEmpty(bpState.CopayCategory), // meansTestCompleted proxy
            null, // meansTestId
            null, // adjustedIncome (would come from means test)
            null, // gmtThreshold
            bpState.IsExemptFromCopay ? "EXEMPT" : null,
            false, // SC 50+ — would come from SC grain
            null,  // SC percent
            false, // VA pension
            bpState.CatastrophicallyDisabled,
            false, // POW
            false, // Purple Heart
            determinedByUserId, determinedByUserName);

        // Auto-apply: update copay exemption if determination says exempt
        if (result.Result == GrainStates.EligibilityDeterminationResult.Exempt && !bpState.IsExemptFromCopay)
        {
            await bp.EnsureInitializedAsync(PatientId);
            await bp.SetCopayExemptionAsync(true, result.CopayExemptionReason ?? "AUTO", DateTime.UtcNow, null);
            result.AutoApplied = true;
        }

        return result;
    }

    public Task<GrainStates.AutoEligibilityDeterminationState> GetAutoEligibilityDeterminationAsync()
        => EligibilityDetermination().GetAsync();

    // ─── TOP Federal Debt Matching — RCTP*.m, RCTOP*.m ──────────────────────

    private ITopMatchIndexGrain TopMatchIndex()
        => GrainFactory.GetGrain<ITopMatchIndexGrain>("TOP-MATCH-IDX");

    public async Task<string> ProcessTopOffsetMatchAsync(
        string treasuryTransactionId, string taxpayerIdNumber, string treasuryPatientName,
        decimal offsetAmount, string offsetSource, DateTime offsetReceivedDate,
        string? processedByUserId, string? processedByUserName, string? notes)
    {
        // Create the match record
        string matchId = Guid.NewGuid().ToString();
        ITopMatchingGrain match = GrainFactory.GetGrain<ITopMatchingGrain>($"TOP-MATCH:{matchId}");
        await match.RecordOffsetAsync(
            treasuryTransactionId, taxpayerIdNumber, treasuryPatientName,
            offsetAmount, offsetSource, offsetReceivedDate, notes);

        // Try to match against this patient's TOP referrals
        ITopReferralIndexGrain topIndex = GrainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-INDEX");
        List<GrainStates.TopReferralIndexEntry> pendingRefs = await topIndex.GetPendingAsync();

        // Find a referral for this patient
        GrainStates.TopReferralIndexEntry? matchedRef = pendingRefs
            .FirstOrDefault(r => r.PatientId == PatientId &&
                (r.Status == GrainStates.TopReferralStatus.Pending || r.Status == GrainStates.TopReferralStatus.Certified));

        if (matchedRef is not null)
        {
            // Match found — apply offset to the AR account
            ITopReferralGrain referral = GrainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{matchedRef.ReferralId}");
            await referral.RecordOffsetAsync(offsetAmount, DateTime.UtcNow);

            IARAccountGrain arAccount = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{matchedRef.ARAccountId}");
            await arAccount.RecordTopOffsetAsync(offsetAmount, processedByUserId ?? "SYSTEM", processedByUserName ?? "TOP-AUTO");

            await match.MatchToAccountAsync(
                PatientId, matchedRef.ARAccountId, matchedRef.ReferralId,
                offsetAmount, processedByUserId, processedByUserName);
        }
        else
        {
            // No matching referral found
            await match.MarkUnmatchedAsync(
                new List<string> { $"No pending TOP referral found for patient {PatientId}" },
                processedByUserId);
        }

        // Update the TOP match index
        GrainStates.TopMatchingState matchState = await match.GetAsync();
        await TopMatchIndex().AddOrUpdateAsync(new GrainStates.TopMatchIndexEntry
        {
            MatchId              = matchId,
            TreasuryTransactionId = treasuryTransactionId,
            TaxpayerIdNumber     = taxpayerIdNumber,
            MatchedPatientId     = matchState.MatchedPatientId,
            OffsetAmount         = offsetAmount,
            Status               = matchState.Status,
            OffsetSource         = offsetSource,
            OffsetReceivedDate   = offsetReceivedDate,
        });

        return matchId;
    }

    public Task<List<GrainStates.TopMatchIndexEntry>> GetTopMatchRecordsAsync()
        => TopMatchIndex().GetByPatientAsync(PatientId);

    public Task<GrainStates.TopMatchingState> GetTopMatchRecordAsync(string matchId)
        => GrainFactory.GetGrain<ITopMatchingGrain>($"TOP-MATCH:{matchId}").GetAsync();
}
