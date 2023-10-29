#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.D2KSmugglers.Graphics;
using OpenRA.Mods.D2KSmugglers.Traits.Air;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.D2KSmugglers.Traits.SupportPowers
{
	public class VulturePowerInfo : SupportPowerInfo
	{
		[ActorReference(typeof(AircraftInfo))]
		public readonly string UnitType = "badr.bomber";
		public readonly int SquadSize = 1;
		public readonly WVec SquadOffset = new(-1536, 1536, 0);

		public readonly WDist Cordon = new(5120);

		public override object Create(ActorInitializer init) { return new VulturePower(init.Self, this); }
	}

	public class VulturePower : SupportPower
	{
		readonly VulturePowerInfo info;
		OperationVulture operation;

		public VulturePower(Actor self, VulturePowerInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
		{
			self.World.OrderGenerator = new SelectVulturePowerTarget(order, manager, info, MouseButton.Left, self.Owner);
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			base.Activate(self, order, manager);
			SendVultures(self, order.Target.CenterPosition);
		}

		public void SendVultures(Actor self, WPos target)
		{
			operation = new OperationVulture(
				target,
				self.CenterPosition,
				info.Cordon,
				info.UnitType,
				self.Owner,
				info.SquadSize,
				self.World);

			self.World.AddFrameEndTask(_ =>
			{
				PlayLaunchSounds();

				operation.SendVultures(
					info.SquadOffset,
					_ => { });
			});
		}
	}

	public class SelectVulturePowerTarget : SelectGenericPowerTarget
	{
		protected Player player;

		protected WPos position;

		protected string vultureUnitType;

		public SelectVulturePowerTarget(
			string order,
			SupportPowerManager manager,
			VulturePowerInfo info,
			MouseButton button,
			Player player)
			: base(order, manager, info.Cursor, button)
		{
			this.player = player;
			vultureUnitType = info.UnitType;
		}

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (player.Shroud.IsVisible(cell))
			{
				return base.Order(world, cell, worldPixel, mi);
			}

			return Enumerable.Empty<Order>();
		}

		protected bool IsValidCell(CPos cell)
		{
			return player.Shroud.IsVisible(cell);
		}

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			position = world.Map.CenterOfCell(cell);

			// return IsValidCell(cell) ? base.GetCursor(world, cell, worldPixel, mi) : "generic-blocked";
			return IsValidCell(cell) ? "default" : "generic-blocked";
		}

		protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
		{
			foreach (var item in base.RenderAnnotations(wr, world))
			{
				yield return item;
			}

			var heavyColor = Color.FromArgb(0, 0, 0);
			var lightColor = Color.FromArgb(130, 101, 82);

			if (IsValidCell(world.Map.CellContaining(position)))
				yield return new OperationVultureTargetRegion(position, 2, lightColor,
					heavyColor, heavyColor);
		}
	}
}
