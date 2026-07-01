// FredderslyPLD Test Version: 2026-07-01.3-ScrapLobPull

namespace RotationSolver.ExtraRotations.Tank;

[Rotation("FredderslyPLD", CombatType.PvE, GameVersion = "7.5", Description = "Johann Collin-inspired Paladin priority model for Lindwurm-style Fight or Flight windows.")]
[SourceCode(Path = "main/ExtraRotations/Tank/FredderslyPLD.cs")]
[ExtraRotation]
public sealed class FredderslyPLD : PaladinRotation
{
	#region Properties

	private static bool InConfiteorCombo => BladeOfFaithReady || BladeOfTruthReady || BladeOfValorReady;
	private static bool HasJohannBurstGCD => HasConfiteorReady || InConfiteorCombo;

	private bool CanUseFightOrFlight => HasHostilesInRange || !MeleeFoF;
	private bool IsStartingFightOrFlight => HasFightOrFlight || IsLastAbility(true, FightOrFlightPvE);

	private bool FightOrFlightSoon => !HasFightOrFlight
		&& CanUseFightOrFlight
		&& FightOrFlightPvE.Cooldown.IsCoolingDown
		&& FightOrFlightPvE.Cooldown.WillHaveOneChargeGCD(2);

	private bool ImperatorAlignedForFightOrFlight => !ImperatorPvE.EnoughLevel
		|| !ImperatorPvE.IsEnabled
		|| !ImperatorPvE.Cooldown.IsCoolingDown
		|| ImperatorPvE.Cooldown.WillHaveOneCharge(1.5f);

	private bool ImperatorReadyForJohannBurst => ImperatorPvE.EnoughLevel
		&& ImperatorPvE.IsEnabled
		&& (!ImperatorPvE.Cooldown.IsCoolingDown || ImperatorPvE.Cooldown.WillHaveOneCharge(3f));

	private bool ShouldWaitForImperator => IsStartingFightOrFlight
		&& !HasJohannBurstGCD
		&& ImperatorReadyForJohannBurst;

	private bool ShouldHoldDamageOGCDsForImperator => IsStartingFightOrFlight
		&& ImperatorPvE.EnoughLevel
		&& ImperatorPvE.IsEnabled
		&& !HasJohannBurstGCD
		&& (!ImperatorPvE.Cooldown.IsCoolingDown || ImperatorPvE.Cooldown.WillHaveOneCharge(5f));

	private bool DamageOGCDsUnlockedByImperator =>
		(FightOrFlightPvE.Cooldown.IsCoolingDown
			&& (ImperatorPvE.EnoughLevel && ImperatorPvE.Cooldown.IsCoolingDown || !ImperatorPvE.EnoughLevel))
		|| (!FightOrFlightPvE.Cooldown.IsCoolingDown && (!ImperatorPvE.EnoughLevel || !ImperatorPvE.Cooldown.IsCoolingDown));

	private bool ShouldHoldAtonementForFightOrFlight => ShouldHoldForFightOrFlight(StatusID.AtonementReady)
		&& !RoyalAuthorityPvE.CanUse(out _, skipComboCheck: false)
		&& !RageOfHalonePvE.CanUse(out _, skipComboCheck: false);

	private bool ShouldHoldSupplicationForFightOrFlight => ShouldHoldForFightOrFlight(StatusID.SupplicationReady);
	private bool ShouldHoldSepulchreForFightOrFlight => ShouldHoldForFightOrFlight(StatusID.SepulchreReady);
	private bool ShouldHoldDivineMightForFightOrFlight => ShouldHoldForFightOrFlight(StatusID.DivineMight);
	private bool ShouldSpendInterveneForDamage => HasFightOrFlight
		&& InterveneChargesToSpendInFightOrFlight > 0
		&& IntervenePvE.Cooldown.CurrentCharges > IntervenePvE.Cooldown.MaxCharges - InterveneChargesToSpendInFightOrFlight;

	#endregion

	#region Config Options

	[RotationConfig(CombatType.PvE, Name = "Only use Fight or Flight while in melee range of an enemy")]
	public bool MeleeFoF { get; set; } = true;

	[Range(0, 2, ConfigUnitType.None, 1)]
	[RotationConfig(CombatType.PvE, Name = "Intervene charges to spend during Fight or Flight")]
	private int InterveneChargesToSpendInFightOrFlight { get; set; } = 1;

	[RotationConfig(CombatType.PvE, Name = "Use Divine Veil during countdown")]
	public bool DivineVeilCountdown { get; set; } = false;

	[RotationConfig(CombatType.PvE, Name = "Use Holy Spirit when out of melee range")]
	private bool UseHolyWhenAway { get; set; } = false;

	[Range(0, 100, ConfigUnitType.Pixels)]
	[RotationConfig(CombatType.PvE, Name = "Use Sheltron at minimum X Oath to prevent over cap (Set to 0 to disable)")]
	private int WhenToSheltron { get; set; } = 100;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Health threshold for Intervention (Set to 0 to disable)")]
	private float InterventionRatio { get; set; } = 0.6f;

	[RotationConfig(CombatType.PvE, Name = "Use Intervention on CoTank during tankbusters")]
	private bool InterventionTank { get; set; } = false;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Health threshold for using Intervention to attempt to save someone")]
	private float InterventionClutch { get; set; } = 0.6f;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Health threshold for Cover (Set to 0 to disable)")]
	private float CoverRatio { get; set; } = 0.3f;

	[RotationConfig(CombatType.PvE, Name = "Use Hallowed Ground with Cover")]
	private bool HallowedWithCover { get; set; } = true;

	[RotationConfig(CombatType.PvE, Name = "Use GCDs to heal. (Ignored if there are no healers alive in party)")]
	public bool GCDHeal { get; set; } = false;

	[RotationConfig(CombatType.PvE, Name = "Use Clemency with Requiescat")]
	private bool RequiescatHealBot { get; set; } = true;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Minimum HP threshold party member needs to be to use Clemency with Requiescat")]
	public float ClemencyRequi { get; set; } = 0.2f;

	[RotationConfig(CombatType.PvE, Name = "Use Clemency without Requiescat")]
	private bool HealBot { get; set; } = true;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Minimum HP threshold party member needs to be to use Clemency without Requiescat")]
	public float ClemencyNoRequi { get; set; } = 0.4f;

	#endregion

	#region Tracking Properties

	public override void DisplayRotationStatus()
	{
		ImGui.Text($"Johann Burst GCD: {HasJohannBurstGCD}");
		ImGui.Text($"Fight or Flight Soon: {FightOrFlightSoon}");
		ImGui.Text($"Waiting for Imperator: {ShouldWaitForImperator}");
		ImGui.Text($"Hold Atonement: {ShouldHoldAtonementForFightOrFlight}");
		ImGui.Text($"Hold Supplication: {ShouldHoldSupplicationForFightOrFlight}");
		ImGui.Text($"Hold Sepulchre: {ShouldHoldSepulchreForFightOrFlight}");
		ImGui.Text($"Hold Divine Might: {ShouldHoldDivineMightForFightOrFlight}");
		ImGui.Text($"Intervene Burst Spend: {InterveneChargesToSpendInFightOrFlight}");
		ImGui.Text($"Intervene Charges: {IntervenePvE.Cooldown.CurrentCharges}");
		ImGui.Text($"Use Oath: {UseOath(out _)}");
	}

	#endregion

	#region Countdown Logic

	protected override IAction? CountDownAction(float remainTime)
	{
		if (DivineVeilCountdown && remainTime < 15 && DivineVeilPvE.CanUse(out var act))
		{
			return act;
		}

		if (remainTime < HolySpiritPvE.Info.CastTime + CountDownAhead
			&& HolySpiritPvE.CanUse(out act))
		{
			return act;
		}

		return base.CountDownAction(remainTime);
	}

	#endregion

	#region oGCD Logic

	[RotationDesc(ActionID.IntervenePvE)]
	protected override bool MoveForwardAbility(IAction nextGCD, out IAction? act)
	{
		return IntervenePvE.CanUse(out act) || base.MoveForwardAbility(nextGCD, out act);
	}

	[RotationDesc(ActionID.DivineVeilPvE, ActionID.PassageOfArmsPvE, ActionID.ReprisalPvE)]
	protected override bool DefenseAreaAbility(IAction nextGCD, out IAction? act)
	{
		if (DivineVeilPvE.CanUse(out act))
		{
			return true;
		}

		if (PassageOfArmsPvE.CanUse(out act))
		{
			return true;
		}

		if (ReprisalPvE.CanUse(out act, skipAoeCheck: true))
		{
			return true;
		}

		return base.DefenseAreaAbility(nextGCD, out act);
	}

	[RotationDesc(ActionID.SentinelPvE, ActionID.RampartPvE, ActionID.BulwarkPvE, ActionID.SheltronPvE, ActionID.HolySheltronPvE, ActionID.ReprisalPvE)]
	protected override bool DefenseSingleAbility(IAction nextGCD, out IAction? act)
	{
		if (InterventionTank && InterventionPvE.CanUse(out act))
		{
			return true;
		}

		if (StatusHelper.PlayerHasStatus(true, StatusID.HallowedGround))
		{
			return base.DefenseSingleAbility(nextGCD, out act);
		}

		if (BulwarkPvE.CanUse(out act, skipAoeCheck: true))
		{
			return true;
		}

		if (UseOath(out act))
		{
			return true;
		}

		if ((!RampartPvE.Cooldown.IsCoolingDown || RampartPvE.Cooldown.ElapsedAfter(60))
			&& GuardianPvE.CanUse(out act) && GuardianPvE.EnoughLevel)
		{
			return true;
		}

		if ((!RampartPvE.Cooldown.IsCoolingDown || RampartPvE.Cooldown.ElapsedAfter(60))
			&& SentinelPvE.CanUse(out act) && !GuardianPvE.EnoughLevel)
		{
			return true;
		}

		if (!SentinelPvE.EnoughLevel && RampartPvE.CanUse(out act))
		{
			return true;
		}

		if (SentinelPvE.EnoughLevel && !GuardianPvE.EnoughLevel
			&& SentinelPvE.Cooldown.IsCoolingDown && SentinelPvE.Cooldown.ElapsedAfter(30)
			&& RampartPvE.CanUse(out act))
		{
			return true;
		}

		if (GuardianPvE.EnoughLevel
			&& GuardianPvE.Cooldown.IsCoolingDown && GuardianPvE.Cooldown.ElapsedAfter(30)
			&& RampartPvE.CanUse(out act))
		{
			return true;
		}

		return ReprisalPvE.CanUse(out act, skipAoeCheck: true) || base.DefenseSingleAbility(nextGCD, out act);
	}

	protected override bool EmergencyAbility(IAction nextGCD, out IAction? act)
	{
		return TryUseFightOrFlight(nextGCD, out act)
			|| TryUseImperator(out act)
			|| TryUseBladeOfHonor(out act)
			|| TryUseJohannPotion(out act)
			|| TryUseEmergencyMitigation(out act)
			|| base.EmergencyAbility(nextGCD, out act);
	}

	[RotationDesc(ActionID.SheltronPvE, ActionID.HolySheltronPvE)]
	protected override bool GeneralAbility(IAction nextGCD, out IAction? act)
	{
		if (InCombat && OathGauge >= WhenToSheltron && WhenToSheltron > 0 && UseOath(out act))
		{
			return true;
		}

		return base.GeneralAbility(nextGCD, out act);
	}

	[RotationDesc(ActionID.ImperatorPvE, ActionID.BladeOfHonorPvE, ActionID.IntervenePvE, ActionID.SpiritsWithinPvE, ActionID.ExpiacionPvE, ActionID.CircleOfScornPvE)]
	protected override bool AttackAbility(IAction nextGCD, out IAction? act)
	{
		if (TryUseImperator(out act))
		{
			return true;
		}

		if (ShouldHoldDamageOGCDsForImperator)
		{
			act = null;
			return false;
		}

		return TryUseBladeOfHonor(out act)
			|| TryUseDamageOGCDs(out act)
			|| base.AttackAbility(nextGCD, out act);
	}

	#endregion

	#region GCD Logic

	[RotationDesc(ActionID.ClemencyPvE)]
	protected override bool HealSingleGCD(out IAction? act)
	{
		if (RequiescatHealBot && RequiescatStacks > 0
			&& ClemencyPvE.CanUse(out act, skipCastingCheck: true)
			&& ClemencyPvE.Target.Target?.GetHealthRatio() < ClemencyRequi)
		{
			return true;
		}

		if (HealBot && ClemencyPvE.CanUse(out act)
			&& ClemencyPvE.Target.Target?.GetHealthRatio() < ClemencyNoRequi)
		{
			return true;
		}

		return base.HealSingleGCD(out act);
	}

	[RotationDesc(ActionID.ShieldBashPvE)]
	protected override bool MyInterruptGCD(out IAction? act)
	{
		if (LowBlowPvE.Cooldown.IsCoolingDown && ShieldBashPvE.CanUse(out act))
		{
			return true;
		}

		return base.MyInterruptGCD(out act);
	}

	protected override bool GeneralGCD(out IAction? act)
	{
		return TryUseJohannBurstGCD(out act)
			|| TryUseActiveConfiteorChain(out act)
			|| TryUseExpiringGoringBlade(out act)
			|| TryUseJohannAtonement(out act)
			|| TryUseJohannFiller(out act)
			|| base.GeneralGCD(out act);
	}

	#endregion

	#region Extra Methods

	#region GCD Skills

	private bool TryUseJohannBurstGCD(out IAction? act)
	{
		act = null;
		if (!HasFightOrFlight)
		{
			return false;
		}

		if (ShouldWaitForImperator)
		{
			return TryUseJohannBurstFiller(out act);
		}

		if (ConfiteorPvE.CanUse(out act, usedUp: true, skipAoeCheck: true))
		{
			return true;
		}

		if (GoringBladePvE.CanUse(out act))
		{
			return true;
		}

		return TryUseJohannBurstFiller(out act);
	}

	private bool TryUseActiveConfiteorChain(out IAction? act)
	{
		act = null;
		return ConfiteorPvE.CanUse(out act, usedUp: true, skipAoeCheck: true);
	}

	private bool TryUseExpiringGoringBlade(out IAction? act)
	{
		act = null;
		return !HasFightOrFlight && GoringBladePvE.CanUse(out act);
	}

	private bool TryUseJohannBurstFiller(out IAction? act)
	{
		act = null;
		return TryUseAtonementContinuation(out act)
			|| TryUseDivineMightHoly(out act);
	}

	private bool TryUseJohannAtonement(out IAction? act)
	{
		act = null;

		if (!ShouldHoldAtonementForFightOrFlight
			&& (!FightOrFlightPvE.Cooldown.WillHaveOneCharge(1) || HasFightOrFlight || StatusHelper.PlayerWillStatusEndGCD(1, 0, true, StatusID.AtonementReady))
			&& AtonementPvE.CanUse(out act))
		{
			return true;
		}

		if (TryUseJohannComboDrift(out act))
		{
			return true;
		}

		if (!ShouldHoldSupplicationForFightOrFlight
			&& (!FightOrFlightPvE.Cooldown.WillHaveOneCharge(1)
				|| RoyalAuthorityPvE.CanUse(out _, skipComboCheck: false) || RageOfHalonePvE.CanUse(out _, skipComboCheck: false)
				|| HasFightOrFlight || StatusHelper.PlayerWillStatusEndGCD(1, 0, true, StatusID.SupplicationReady))
			&& SupplicationPvE.CanUse(out act))
		{
			return true;
		}

		if (RequiescatStacks > 0 && IsLastGCD(true, SupplicationPvE) && !HasFightOrFlight
			&& HolySpiritPvE.CanUse(out act, skipCastingCheck: true))
		{
			return true;
		}

		if (!ShouldHoldSepulchreForFightOrFlight
			&& (!FightOrFlightPvE.Cooldown.WillHaveOneCharge(1)
				|| RoyalAuthorityPvE.CanUse(out _, skipComboCheck: false) || RageOfHalonePvE.CanUse(out _, skipComboCheck: false)
				|| HasFightOrFlight || StatusHelper.PlayerWillStatusEndGCD(1, 0, true, StatusID.SepulchreReady))
			&& SepulchrePvE.CanUse(out act))
		{
			return true;
		}

		return false;
	}

	private bool TryUseJohannComboDrift(out IAction? act)
	{
		act = null;
		if (HasFightOrFlight || StatusHelper.PlayerHasStatus(true, StatusID.Medicated)
			|| RoyalAuthorityPvE.CanUse(out _, skipComboCheck: false)
			|| RageOfHalonePvE.CanUse(out _, skipComboCheck: false)
			|| TotalEclipsePvE.CanUse(out _))
		{
			return false;
		}

		if ((!HasAtonementReady && HasDivineMight && !SupplicationReady && !SepulchreReady)
			|| (HasAtonementReady && !HasDivineMight))
		{
			return RiotBladePvE.CanUse(out act) || FastBladePvE.CanUse(out act);
		}

		return false;
	}

	private bool TryUseAtonementContinuation(out IAction? act)
	{
		act = null;
		return SepulchrePvE.CanUse(out act, skipComboCheck: true)
			|| SupplicationPvE.CanUse(out act, skipComboCheck: true)
			|| AtonementPvE.CanUse(out act);
	}

	private bool TryUseDivineMightHoly(out IAction? act)
	{
		act = null;
		return HasDivineMight
			&& !ShouldHoldDivineMightForFightOrFlight
			&& HolySpiritPvE.CanUse(out act, skipCastingCheck: true);
	}

	private bool TryUseJohannFiller(out IAction? act)
	{
		act = null;

		if ((RequiescatStacks > 0 || HasDivineMight && !ShouldHoldDivineMightForFightOrFlight)
			&& HolyCirclePvE.CanUse(out act, skipCastingCheck: true))
		{
			return true;
		}

		if (ProminencePvE.CanUse(out act, skipStatusProvideCheck: !EnhancedProminenceTrait.EnoughLevel))
		{
			return true;
		}

		if (TotalEclipsePvE.CanUse(out act))
		{
			return true;
		}

		if ((RequiescatStacks > 0 || HasDivineMight && !ShouldHoldDivineMightForFightOrFlight)
			&& HolySpiritPvE.CanUse(out act, skipCastingCheck: true))
		{
			return true;
		}

		if (!SupplicationReady && !SepulchreReady && !HasAtonementReady && !HasDivineMight
			&& RoyalAuthorityPvE.CanUse(out act))
		{
			return true;
		}

		if (RoyalAuthorityPvE.CanUse(out act))
		{
			return true;
		}

		if (!RoyalAuthorityPvE.Info.EnoughLevelAndQuest() && RageOfHalonePvE.CanUse(out act))
		{
			return true;
		}

		if (RiotBladePvE.CanUse(out act))
		{
			return true;
		}

		if (FastBladePvE.CanUse(out act))
		{
			return true;
		}

		if (UseHolyWhenAway && StopMovingTime > 1 && HolySpiritPvE.CanUse(out act))
		{
			return true;
		}

		if (ShieldLobPvE.CanUse(out act))
		{
			return true;
		}

		return false;
	}

	#endregion

	#region oGCD Skills

	private bool TryUseJohannPotion(out IAction? act)
	{
		act = null;
		if (!InCombat)
		{
			return false;
		}

		// Standard opener pots in Riot Blade's weave slot, two GCDs before Fight or Flight.
		if (CombatElapsedLessGCD(3) && IsLastGCD(true, RiotBladePvE)
			&& UseBurstMedicine(out act))
		{
			return true;
		}

		return HasFightOrFlight && UseBurstMedicine(out act);
	}

	private bool TryUseFightOrFlight(IAction nextGCD, out IAction? act)
	{
		act = null;
		return ShouldUseFightOrFlight(nextGCD) && FightOrFlightPvE.CanUse(out act);
	}

	private bool TryUseImperator(out IAction? act)
	{
		act = null;
		return (IsLastAbility(true, FightOrFlightPvE) || HasFightOrFlight)
			&& ImperatorPvE.CanUse(out act, skipAoeCheck: true, usedUp: true, skipTTKCheck: true);
	}

	private bool TryUseBladeOfHonor(out IAction? act)
	{
		act = null;
		return BladeOfHonorPvE.CanUse(out act, skipAoeCheck: true);
	}

	private bool TryUseDamageOGCDs(out IAction? act)
	{
		act = null;

		if (!DamageOGCDsUnlockedByImperator)
		{
			return false;
		}

		if (ExpiacionPvE.CanUse(out act, skipAoeCheck: true, skipTTKCheck: true))
		{
			return true;
		}

		if (!ExpiacionPvE.EnoughLevel && SpiritsWithinPvE.CanUse(out act, skipAoeCheck: true, skipTTKCheck: true))
		{
			return true;
		}

		if (CircleOfScornPvE.CanUse(out act, skipAoeCheck: true, skipTTKCheck: true))
		{
			return true;
		}

		if (!IsMoving && ShouldSpendInterveneForDamage && IntervenePvE.CanUse(out act, usedUp: HasFightOrFlight))
		{
			return true;
		}

		return false;
	}

	private bool TryUseEmergencyMitigation(out IAction? act)
	{
		act = null;

		if (StatusHelper.PlayerHasStatus(true, StatusID.Cover) && HallowedWithCover && HallowedGroundPvE.CanUse(out act))
		{
			return true;
		}

		if (HallowedGroundPvE.CanUse(out act) && Player?.GetHealthRatio() <= HealthForDyingTanks)
		{
			return true;
		}

		if ((StatusHelper.PlayerHasStatus(true, StatusID.Rampart) || StatusHelper.PlayerHasStatus(true, StatusID.Sentinel))
			&& InterventionPvE.CanUse(out act, skipTargetStatusNeedCheck: true)
			&& InterventionPvE.Target.Target?.GetHealthRatio() < InterventionClutch)
		{
			return true;
		}

		return CoverPvE.CanUse(out act)
			&& CoverPvE.Target.Target?.DistanceToPlayer() < 10
			&& CoverPvE.Target.Target?.GetHealthRatio() < CoverRatio;
	}

	#endregion

	#region Miscellaneous Methods

	private bool ShouldUseFightOrFlight(IAction nextGCD)
	{
		if (!InCombat || !CanUseFightOrFlight)
		{
			return false;
		}

		if (!ImperatorAlignedForFightOrFlight)
		{
			return false;
		}

		// Standard opener holds Fight or Flight through the first combo GCDs;
		// the final gate then fires it in Royal Authority's weave slot.
		if (CombatElapsedLessGCD(2))
		{
			return false;
		}

		if (!FastBladePvE.IsEnabled)
		{
			return true;
		}

		if (!RiotBladePvE.EnoughLevel)
		{
			return nextGCD.IsTheSameTo(true, FastBladePvE);
		}

		if (!RageOfHalonePvE.EnoughLevel)
		{
			return nextGCD.IsTheSameTo(true, RiotBladePvE, TotalEclipsePvE);
		}

		if (!ProminencePvE.EnoughLevel)
		{
			return nextGCD.IsTheSameTo(true, RageOfHalonePvE, TotalEclipsePvE);
		}

		if (!AtonementPvE.EnoughLevel)
		{
			return nextGCD.IsTheSameTo(true, RoyalAuthorityPvE, ProminencePvE);
		}

		// IsCoolingDown guard: a never-used Imperator "will have a charge" too, which would
		// fire Fight or Flight before Royal Authority banks procs in the standard opener.
		return (ImperatorPvE.Cooldown.IsCoolingDown && ImperatorPvE.Cooldown.WillHaveOneChargeGCD(1))
			|| HasJohannBurstGCD
			|| StatusHelper.PlayerHasStatus(true, StatusID.AtonementReady, StatusID.SepulchreReady, StatusID.SupplicationReady, StatusID.DivineMight)
			|| IsLastAction(true, RoyalAuthorityPvE)
			|| nextGCD.IsTheSameTo(true, AtonementPvE, SupplicationPvE, SepulchrePvE, HolySpiritPvE);
	}

	private bool ShouldHoldForFightOrFlight(StatusID status)
	{
		return FightOrFlightSoon && !StatusHelper.PlayerWillStatusEndGCD(2, 0, true, status);
	}

	private bool UseOath(out IAction? act)
	{
		if (InterventionPvE.Target.Target?.GetHealthRatio() <= InterventionRatio && InterventionPvE.CanUse(out act))
		{
			return true;
		}

		if (HolySheltronPvE.CanUse(out act) && HolySheltronPvE.EnoughLevel)
		{
			return true;
		}

		if (SheltronPvE.CanUse(out act) && !HolySheltronPvE.EnoughLevel)
		{
			return true;
		}

		return false;
	}

	public override bool CanHealSingleSpell
	{
		get
		{
			var aliveHealerCount = 0;
			var healers = PartyMembers.GetJobCategory(JobRole.Healer);
			foreach (var h in healers)
			{
				if (!h.IsDead)
				{
					aliveHealerCount++;
				}
			}

			return base.CanHealSingleSpell && (GCDHeal || aliveHealerCount == 0);
		}
	}

	#endregion

	#endregion
}
