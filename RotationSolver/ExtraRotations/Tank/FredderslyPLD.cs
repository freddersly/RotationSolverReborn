using System.ComponentModel;

namespace RotationSolver.ExtraRotations.Tank;

[Rotation("FredderslyPLD", CombatType.PvE, GameVersion = "7.5", Description = "High-end Paladin Fight or Flight planner based on the Johann, Hidey, and Lilith Lindwurm logs.")]
[SourceCode(Path = "main/ExtraRotations/Tank/FredderslyPLD.cs")]
[ExtraRotation]
public sealed class FredderslyPLD : PaladinRotation
{
	#region Rotation State

	private static bool MidBasicCombo => LiveComboTime > 0f
		&& IsLastComboAction(ActionID.FastBladePvE, ActionID.RiotBladePvE);

	private bool IsJohannStyle => ParserStyle == TopParserFoFStyle.JohannRecommended;
	private bool IsHideyStyle => ParserStyle == TopParserFoFStyle.HideyHiagal;
	private bool IsLilithStyle => ParserStyle == TopParserFoFStyle.LilithOmgBestie;

	private bool HasCarryoverAtonement => SupplicationReady || SepulchreReady;

	private bool ShouldHoldBladeOfHonorForLilith => IsLilithStyle
		&& HasFightOrFlight
		&& GoringBladePvE.CanUse(out _)
		&& BladeOfHonorPvE.CanUse(out _)
		&& !BladeOfValorPvE.CanUse(out _, skipComboCheck: true);

	#endregion

	#region Configuration

	private enum TopParserFoFStyle : byte
	{
		[Description("Johann Collin (Recommended) — Finish the Confiteor blades, then Goring Blade")]
		JohannRecommended,

		[Description("Hidey Hiagal — Goring Blade first, then any carried Supplication/Sepulchre, then Confiteor")]
		HideyHiagal,

		[Description("Lilith Omg-bestie — Goring Blade between Blade of Valor and Blade of Honor")]
		LilithOmgBestie,
	}

	[RotationConfig(CombatType.PvE, Name = "Top parser Fight or Flight style")]
	private TopParserFoFStyle ParserStyle { get; set; } = TopParserFoFStyle.JohannRecommended;

	[RotationConfig(CombatType.PvE, Name = "Use 2 Intervene charges during Fight or Flight")]
	private bool DumpInterveneInFoF { get; set; } = true;

	[RotationConfig(CombatType.PvE, Name = "Use Holy Spirit for ranged downtime")]
	private bool UseHolyWhenAway { get; set; } = true;

	#endregion

	#region Tracking Properties

	public override void DisplayRotationStatus()
	{
		ImGui.Text($"FoF Style: {ParserStyle}");
		ImGui.Text($"In Fight or Flight: {HasFightOrFlight}");
		ImGui.Text($"Goring Ready: {GoringBladePvE.CanUse(out _)}");
		ImGui.Text($"Confiteor Ready: {HasConfiteorReady}");
		ImGui.Text($"Atonement Ready: {HasAtonementReady}");
		ImGui.Text($"Supplication Ready: {SupplicationReady}");
		ImGui.Text($"Sepulchre Ready: {SepulchreReady}");
		ImGui.Text($"Mid Basic Combo: {MidBasicCombo}");
	}

	#endregion

	#region Countdown Logic

	protected override IAction? CountDownAction(float remainTime)
	{
		if (remainTime <= 2.0f && HolySpiritPvE.CanUse(out var act))
		{
			return act;
		}

		if (remainTime <= 1.2f && UseBurstMedicine(out act))
		{
			return act;
		}

		return base.CountDownAction(remainTime);
	}

	#endregion

	#region Continuation and Burst oGCD Logic

	private bool TryUseBladeOfHonor(out IAction? act)
	{
		act = null;

		if (ShouldHoldBladeOfHonorForLilith)
		{
			return false;
		}

		return BladeOfHonorPvE.CanUse(out act);
	}

	private bool TryUseFightOrFlight(out IAction? act)
	{
		act = null;
		if (!FightOrFlightPvE.CanUse(out act))
		{
			return false;
		}

		if (CombatElapsedLessGCD(4))
		{
			return IsJohannStyle || IsLastGCD(ActionID.RoyalAuthorityPvE);
		}

		return true;
	}

	private bool TryUseImperator(out IAction? act)
	{
		act = null;
		return HasFightOrFlight
			&& ImperatorPvE.CanUse(out act, skipAoeCheck: true, usedUp: true, skipTTKCheck: true);
	}

	private bool TryUseBurstPotion(out IAction? act)
	{
		act = null;
		return HasFightOrFlight && InCombat && UseBurstMedicine(out act);
	}

	private bool TryUseDamageOGCDs(out IAction? act)
	{
		act = null;

		if (ExpiacionPvE.CanUse(out act, skipAoeCheck: true))
		{
			return true;
		}

		if (CircleOfScornPvE.CanUse(out act, skipAoeCheck: true))
		{
			return true;
		}

		if (HasFightOrFlight && IntervenePvE.CanUse(out act, usedUp: DumpInterveneInFoF))
		{
			return true;
		}

		return false;
	}

	protected override bool EmergencyAbility(IAction nextGCD, out IAction? act)
	{
		return TryUseBladeOfHonor(out act)
			|| base.EmergencyAbility(nextGCD, out act);
	}

	protected override bool AttackAbility(IAction nextGCD, out IAction? act)
	{
		return TryUseFightOrFlight(out act)
			|| TryUseImperator(out act)
			|| TryUseBurstPotion(out act)
			|| TryUseDamageOGCDs(out act)
			|| base.AttackAbility(nextGCD, out act);
	}

	#endregion

	#region Defense and Movement Logic

	[RotationDesc(ActionID.DivineVeilPvE, ActionID.ReprisalPvE)]
	protected override bool DefenseAreaAbility(IAction nextGCD, out IAction? act)
	{
		if (DivineVeilPvE.CanUse(out act))
		{
			return true;
		}

		if (ReprisalPvE.CanUse(out act, skipAoeCheck: true))
		{
			return true;
		}

		return base.DefenseAreaAbility(nextGCD, out act);
	}

	[RotationDesc(ActionID.HolySheltronPvE, ActionID.InterventionPvE, ActionID.GuardianPvE, ActionID.RampartPvE)]
	protected override bool DefenseSingleAbility(IAction nextGCD, out IAction? act)
	{
		if (HolySheltronPvE.CanUse(out act))
		{
			return true;
		}

		if (InterventionPvE.CanUse(out act, targetOverride: TargetType.Tankbuster))
		{
			return true;
		}

		if ((!RampartPvE.Cooldown.IsCoolingDown || RampartPvE.Cooldown.ElapsedAfter(60))
			&& GuardianPvE.CanUse(out act))
		{
			return true;
		}

		if (GuardianPvE.Cooldown.IsCoolingDown && GuardianPvE.Cooldown.ElapsedAfter(30)
			&& RampartPvE.CanUse(out act))
		{
			return true;
		}

		if (ReprisalPvE.CanUse(out act, skipAoeCheck: true))
		{
			return true;
		}

		return base.DefenseSingleAbility(nextGCD, out act);
	}

	[RotationDesc(ActionID.IntervenePvE)]
	protected override bool MoveForwardAbility(IAction nextGCD, out IAction? act)
	{
		return IntervenePvE.CanUse(out act) || base.MoveForwardAbility(nextGCD, out act);
	}

	#endregion

	#region GCD Phase Logic

	protected override bool GeneralGCD(out IAction? act)
	{
		if (HasFightOrFlight && TryUseSelectedFoFBurstGCD(out act))
		{
			return true;
		}

		if (TryUseActiveConfiteorChain(out act))
		{
			return true;
		}

		if (TryUseExpiringBurstReady(out act))
		{
			return true;
		}

		if (TryUseAtonementFillerGCD(out act))
		{
			return true;
		}

		if (TryUseFillerGCD(out act))
		{
			return true;
		}

		return base.GeneralGCD(out act);
	}

	#endregion

	#region Fight or Flight Burst Profiles

	private bool TryUseSelectedFoFBurstGCD(out IAction? act)
	{
		return ParserStyle switch
		{
			TopParserFoFStyle.JohannRecommended => TryUseJohannFoFGCD(out act),
			TopParserFoFStyle.HideyHiagal => TryUseHideyFoFGCD(out act),
			TopParserFoFStyle.LilithOmgBestie => TryUseLilithFoFGCD(out act),
			_ => TryUseJohannFoFGCD(out act),
		};
	}

	private bool TryUseJohannFoFGCD(out IAction? act)
	{
		act = null;

		if (!HasConfiteorReady && ImperatorPvE.CanUse(out _))
		{
			return TryUseFoFFillers(out act);
		}

		if (ConfiteorPvE.CanUse(out act, usedUp: true, skipAoeCheck: true))
		{
			return true;
		}

		if (GoringBladePvE.CanUse(out act))
		{
			return true;
		}

		return TryUseFoFFillers(out act);
	}

	private bool TryUseHideyFoFGCD(out IAction? act)
	{
		act = null;

		if (GoringBladePvE.CanUse(out act))
		{
			return true;
		}

		if (HasCarryoverAtonement && TryUseAtonementContinuation(out act))
		{
			return true;
		}

		if (ConfiteorPvE.CanUse(out act, usedUp: true, skipAoeCheck: true))
		{
			return true;
		}

		return TryUseFoFFillers(out act);
	}

	private bool TryUseLilithFoFGCD(out IAction? act)
	{
		act = null;

		if (BladeOfValorPvE.CanUse(out act, skipComboCheck: true))
		{
			return true;
		}

		if (BladeOfHonorPvE.CanUse(out _) && GoringBladePvE.CanUse(out act))
		{
			return true;
		}

		if (!HasConfiteorReady && ImperatorPvE.CanUse(out _))
		{
			return TryUseFoFFillers(out act);
		}

		if (ConfiteorPvE.CanUse(out act, usedUp: true, skipAoeCheck: true))
		{
			return true;
		}

		if (GoringBladePvE.CanUse(out act))
		{
			return true;
		}

		return TryUseFoFFillers(out act);
	}

	private bool TryUseFoFFillers(out IAction? act)
	{
		act = null;

		if (TryUseAtonementContinuation(out act))
		{
			return true;
		}

		if (HasDivineMight && HolySpiritPvE.CanUse(out act, skipCastingCheck: true))
		{
			return true;
		}

		return false;
	}

	#endregion

	#region Active Combo and Ready-State Recovery

	private bool TryUseActiveConfiteorChain(out IAction? act)
	{
		act = null;

		return ConfiteorPvE.CanUse(out act, usedUp: true, skipAoeCheck: true);
	}

	private bool TryUseExpiringBurstReady(out IAction? act)
	{
		act = null;

		if (!HasFightOrFlight && GoringBladePvE.CanUse(out act))
		{
			return true;
		}

		return false;
	}

	#endregion

	#region Atonement and Filler Logic

	private bool TryUseAtonementContinuation(out IAction? act)
	{
		act = null;
		return SepulchrePvE.CanUse(out act, skipComboCheck: true)
			|| SupplicationPvE.CanUse(out act, skipComboCheck: true)
			|| AtonementPvE.CanUse(out act);
	}

	private bool TryUseAtonementFillerGCD(out IAction? act)
	{
		act = null;

		if ((!FightOrFlightPvE.Cooldown.WillHaveOneCharge(1) || HasFightOrFlight
			|| StatusHelper.PlayerWillStatusEndGCD(1, 0, true, StatusID.AtonementReady))
			&& AtonementPvE.CanUse(out act))
		{
			return true;
		}

		if ((RoyalAuthorityPvE.CanUse(out _, skipComboCheck: false) || HasFightOrFlight
			|| StatusHelper.PlayerWillStatusEndGCD(1, 0, true, StatusID.SupplicationReady))
			&& SupplicationPvE.CanUse(out act))
		{
			return true;
		}

		if (RequiescatStacks > 0 && IsLastGCD(true, SupplicationPvE) && !HasFightOrFlight
			&& HolySpiritPvE.CanUse(out act, skipCastingCheck: true))
		{
			return true;
		}

		if ((RoyalAuthorityPvE.CanUse(out _, skipComboCheck: false) || HasFightOrFlight
			|| StatusHelper.PlayerWillStatusEndGCD(1, 0, true, StatusID.SepulchreReady))
			&& SepulchrePvE.CanUse(out act))
		{
			return true;
		}

		return false;
	}

	private bool TryUseFillerGCD(out IAction? act)
	{
		act = null;

		if ((HasDivineMight || RequiescatStacks > 0)
			&& HolyCirclePvE.CanUse(out act, skipCastingCheck: true))
		{
			return true;
		}

		if (ProminencePvE.CanUse(out act))
		{
			return true;
		}

		if (TotalEclipsePvE.CanUse(out act))
		{
			return true;
		}

		if ((HasDivineMight || RequiescatStacks > 0)
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
}
