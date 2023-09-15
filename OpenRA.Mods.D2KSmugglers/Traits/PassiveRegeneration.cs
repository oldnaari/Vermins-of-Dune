#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.D2KSmugglers.Traits
{
	public class PassiveRegenerationInfo : TraitInfo
	{
		[Desc("Amount")]
		public readonly int Amount = 50;
		public override object Create(ActorInitializer init) { return new PassiveRegeneration(init, this); }
	}

	public class PassiveRegeneration : ITick
	{
		public readonly PassiveRegenerationInfo Info;

		public PassiveRegeneration(ActorInitializer init, PassiveRegenerationInfo info)
		{
			Info = info;
		}

		void ITick.Tick(Actor self)
		{
			var health = self.TraitsImplementing<IHealth>().First();
			health.InflictDamage(self, self, new Damage(-Info.Amount), true);
		}
	}
}
