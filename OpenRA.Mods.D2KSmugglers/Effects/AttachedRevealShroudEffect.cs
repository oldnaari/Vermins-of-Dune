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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.D2KSmugglers.Effects
{
	public class AttachedRevealShroudEffect : IEffect
	{
		static readonly PPos[] NoCells = Array.Empty<PPos>();

		public readonly Target Target;
		readonly Player player;
		readonly Shroud.SourceType sourceType;
		readonly WDist revealRadius;
		readonly PlayerRelationship validStances;
		readonly int duration;

		int ticks;

		public AttachedRevealShroudEffect(Target target, WDist radius, Shroud.SourceType type, Player forPlayer, PlayerRelationship stances, int delay = 0, int duration = 50)
		{
			Target = target;
			player = forPlayer;
			revealRadius = radius;
			validStances = stances;
			sourceType = type;
			this.duration = duration;
			ticks = -delay;
		}

		void AddCellsToPlayerShroud(Player p, PPos[] uv)
		{
			if (validStances.HasRelationship(player.RelationshipWith(p)))
				p.Shroud.AddSource(this, sourceType, uv);
		}

		void RemoveCellsFromPlayerShroud(Player p) { p.Shroud.RemoveSource(this); }

		PPos[] ProjectedCells(World world)
		{
			var map = world.Map;
			var range = revealRadius;
			if (range == WDist.Zero)
				return NoCells;

			return Shroud.ProjectedCellsInRange(map, Target.CenterPosition, WDist.Zero, range).ToArray();
		}

		public void Tick(World world)
		{
			if (ticks == duration || Target.Type == TargetType.Invalid)
			{
				world.AddFrameEndTask(w => w.Remove(this));
				foreach (var p in world.Players)
					RemoveCellsFromPlayerShroud(p);

				return;
			}

			if (ticks != 0)
			{
				foreach (var p in world.Players)
					RemoveCellsFromPlayerShroud(p);
			}

			if (ticks != duration)
			{
				var cells = ProjectedCells(world);
				foreach (var p in world.Players)
					AddCellsToPlayerShroud(p, cells);
			}

			ticks++;
		}

		public void Terminate(World world)
		{
			world.AddFrameEndTask(w => w.Remove(this));
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr) { return SpriteRenderable.None; }
	}
}
