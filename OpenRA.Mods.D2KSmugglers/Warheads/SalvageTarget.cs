using System;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.D2KSmugglers.Warheads
{
	public class SalvageTargetWarhead : SpreadDamageWarhead
	{
		[Desc("The percentage of the damage that will be returned as resources.")]
		public readonly int ResourceYield = 20;

		[Desc("The weapon to use for returning resources")]
		public readonly string WeaponYieldInfo = "DecomposeYield";

		const int SalvageResourceMultiplier = 100;
		protected override void InflictDamage(Actor victim, Actor firedBy, HitShape shape, WarheadArgs args)
		{
			var victimMaxHp = victim.Info.TraitInfo<HealthInfo>().HP;
			var victimCost = victim.Info.TraitInfo<ValuedInfo>().Cost;

			var damage = Damage * DamageVersus(victim, shape, args) / 100;

			// damage = Util.ApplyPercentageModifiers(damage, args.DamageModifiers);
			var healthBeforeDamage = victim.Trait<Health>().HP;

			victim.InflictDamage(firedBy, new Damage(damage, DamageTypes));

			var healthAfterDamage = victim.Trait<Health>().HP;

			var salvageGain = (long)SalvageResourceMultiplier * ResourceYield * (healthBeforeDamage - healthAfterDamage) * victimCost / victimMaxHp / 100;

			salvageGain = salvageGain > 0 ? salvageGain : 0;

			WPos MuzzlePosition() => victim.CenterPosition;

			var firedByMobileTrait = firedBy.Trait<IFacing>();
			WAngle MuzzleFacing() => firedByMobileTrait.Facing;
			victim.World.Map.Rules.Weapons.TryGetValue(WeaponYieldInfo.ToLowerInvariant(),
				out var weaponYield);

			var argsReturn = new ProjectileArgs
			{
				Weapon = weaponYield,
				Facing = MuzzleFacing(),
				CurrentMuzzleFacing = MuzzleFacing,
				DamageModifiers = new[] { (int)salvageGain },
				InaccuracyModifiers = Array.Empty<int>(),
				RangeModifiers = Array.Empty<int>(),
				Source = MuzzlePosition(),
				CurrentSource = MuzzlePosition,
				SourceActor = victim,
				PassiveTarget = firedBy.CenterPosition,
				GuidedTarget = Target.FromActor(firedBy)
			};
			var projectile = weaponYield.Projectile.Create(argsReturn);
			if (projectile != null)
				firedBy.World.AddFrameEndTask(w => w.Add(projectile));
		}

		public override bool IsValidAgainst(Actor victim, Actor firedBy)
		{
			// Cannot be damaged without a Health trait
			if (!victim.Info.HasTraitInfo<IHealthInfo>())
				return false;

			// Cannot be damaged without a Valued trait
			if (!victim.Info.HasTraitInfo<ValuedInfo>())
				return false;

			return base.IsValidAgainst(victim, firedBy);
		}
	}
}
