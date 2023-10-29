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
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.D2KSmugglers.Traits.Air
{
	public class FlyVultureInfo : AttackBaseInfo
	{
		public override object Create(ActorInitializer init) { return new FlyVulture(init.Self, this); }
	}

	public enum OperationStateType
	{
		Approach,
		Harvest,
		Return,
		Drop
	}

	public class OperationVulture : ISync
	{
		public static readonly WDist PickUpDistance = new(1024);

		public World World { get; }

		public WAngle AttackAngle { get; }

		public string UnitType { get; }

		public WPos CheckpointStart { get; }
		public WPos CheckpointTarget { get; }
		public WPos CheckpointDropPoint { get; }

		public Player Owner { get; }
		public int LoopPeriodInTicks { get; }
		public int SquadSize { get; }
		public int VultureOffset { get; }
		public int LoopRadius { get; }

		public OperationVulture(
			WPos target,
			WPos drop,
			WDist cordon,
			string unitType,
			Player owner,
			int squadSize,
			World world)
		{
			UnitType = unitType;
			Owner = owner;
			SquadSize = squadSize;
			World = world;

			var altitude = World.Map.Rules.Actors[UnitType].TraitInfo<AircraftInfo>().CruiseAltitude.Length;
			var attackDirection = target - drop;
			attackDirection = new WVec(attackDirection.X, attackDirection.Y, 0);
			AttackAngle = WAngle.ArcTan(attackDirection.X, attackDirection.Y);

			// Bring attackDirection to facing quantization
			attackDirection = new WVec(0, 1024, 0).Rotate(WRot.FromYaw(AttackAngle));

			CheckpointStart = target - (World.Map.DistanceToEdge(target, -attackDirection)
				+ cordon).Length * attackDirection / attackDirection.Length;

			CheckpointTarget = target + new WVec(0, 0, altitude);
			CheckpointStart += new WVec(0, 0, altitude);
			CheckpointDropPoint = drop + new WVec(0, 0, altitude);

			var rules = World.Map.Rules;
			var aircraft = rules.Actors[UnitType].TraitInfo<AircraftInfo>();
			LoopPeriodInTicks = 1024 / aircraft.TurnSpeed.Angle;
			VultureOffset = aircraft.Speed * LoopPeriodInTicks * (SquadSize + 1) / SquadSize;
			LoopRadius = aircraft.Speed * LoopPeriodInTicks * 100 / 628;
		}

		public static int ComputeRadiusForUnitType(Ruleset rules, string unitType)
		{
			var aircraft = rules.Actors[unitType].TraitInfo<AircraftInfo>();
			var loopPeriodInTicks = 1024 / aircraft.TurnSpeed.Angle;
			return aircraft.Speed * loopPeriodInTicks * 100 / 628;
		}

		public void SendVultures(
			WVec squadOffset,
			Action<Actor> onRemovedFromWorld)
		{
			for (var i = 0; i < SquadSize; i++)
			{
				// Includes the 90 degree rotation between body and world coordinates
				var spawnOffsetShift = new WVec(0, -1024, 0).Rotate(WRot.FromYaw(AttackAngle));
				var targetOffset = new WVec(-LoopRadius, 0, 0).Rotate(WRot.FromYaw(AttackAngle));

				var vulture = World.CreateActor(false, UnitType, new TypeDictionary
				{
					new CenterPositionInit(CheckpointStart + spawnOffsetShift * i * VultureOffset / 1024 + targetOffset),
					new OwnerInit(Owner),
					new FacingInit(AttackAngle),
				});

				var flyVulture = vulture.Trait<FlyVulture>();
				flyVulture.OnRemovedFromWorld += onRemovedFromWorld;
				flyVulture.Operation = this;

				World.Add(vulture);

				vulture.QueueActivity(new Fly(vulture, Target.FromPos(CheckpointTarget + targetOffset)));
				vulture.QueueActivity(new CallFunc(() => vulture.Trait<FlyVulture>().State = OperationStateType.Harvest));
				vulture.QueueActivity(new FlyIdle(vulture, 400 - i * LoopPeriodInTicks));
				vulture.QueueActivity(new CallFunc(() => vulture.Trait<FlyVulture>().State = OperationStateType.Return));
				vulture.QueueActivity(new Fly(vulture, Target.FromPos(CheckpointDropPoint)));
				vulture.QueueActivity(new CallFunc(() => vulture.Trait<FlyVulture>().State = OperationStateType.Drop));
				vulture.QueueActivity(new Fly(vulture, Target.FromPos(CheckpointStart)));
				vulture.QueueActivity(new RemoveSelf());
			}
		}

		public bool IsValidTarget(Actor target)
		{
			if (Owner.RelationshipWith(target.Owner) != PlayerRelationship.Enemy)
			{
				return false;
			}

			if (target.IsDead)
			{
				return false;
			}

			if (target.TraitsImplementing<Carryable>().ToList().Count == 0)
			{
				return false;
			}

			if (target.TraitsImplementing<Mobile>().ToList().Count == 0)
			{
				return false;
			}

			if (IsDockedHarvester(target))
			{
				return false;
			}

			if (!target.TraitsImplementing<IDefaultVisibility>().ToList().First().IsVisible(target, Owner))
			{
				return false;
			}

			return true;
		}

		static bool IsDockedHarvester(Actor other)
		{
			if (other.TraitsImplementing<Harvester>().ToList().Count != 0)
				return other.Trait<WithSpriteBody>().DefaultAnimation.CurrentSequence.Name == "dock-loop";
			else
				return false;
		}

		public IEnumerable<Actor> GetUnitsInBlock(WPos blockCenterPosition, WDist squareSize, WAngle angle, int speed)
		{
			var diagonal = squareSize * 141 / 100;
			var candidates = World.FindActorsInCircle(blockCenterPosition, diagonal);
			var rotation = WRot.FromYaw(-angle);

			bool Selector(Actor a)
			{
				var diff = (a.CenterPosition - blockCenterPosition).Rotate(rotation);

				var isYInRange = Math.Abs(diff.Y) < squareSize.Length + speed;
				var isXInRange = Math.Abs(diff.X) < squareSize.Length;

				return isXInRange && isYInRange;
			}

			return candidates.Where(Selector);
		}

		public void CleanUp()
		{
		}
	}

	public class FlyVulture : AttackBase, ITick, INotifyRemovedFromWorld
	{
		public event Action<Actor> OnRemovedFromWorld = _ => { };

		public OperationVulture Operation;
		public OperationStateType State;

		public FlyVulture(Actor self, FlyVultureInfo info)
			: base(self, info)
		{
			State = OperationStateType.Approach;
		}

		void HarvestTick(Actor self)
		{
			var pickUpDistance = OperationVulture.PickUpDistance.Length;

			var carryallTrait = self.Trait<Carryall>();

			if (carryallTrait.Carryable != null)
				return;

			var candidatesIterable = Operation.GetUnitsInBlock(
				self.CenterPosition,
				new WDist(pickUpDistance),
				self.Trait<Aircraft>().Facing,
				self.Trait<Aircraft>().MovementSpeed);

			candidatesIterable = candidatesIterable.Where(Operation.IsValidTarget);

			var candidates = candidatesIterable.ToList();

			if (candidates.Count == 0)
				return;

			var closestTarget = candidates.MaxBy(a => -(self.CenterPosition - a.CenterPosition).LengthSquared);

			self.World.AddFrameEndTask(_ =>
			{
				closestTarget.ChangeOwnerSync(self.Owner);
				if (closestTarget.TraitsImplementing<Harvester>().ToList().Count != 0)
					closestTarget.Trait<Harvester>().ChooseNewProc(closestTarget, null);
				closestTarget.CancelActivity();
				closestTarget.World.Remove(closestTarget);
				closestTarget.Trait<Carryable>().Attached(closestTarget);
				carryallTrait.AttachCarryable(self, closestTarget);
			});
		}

		void DropTick(Actor self)
		{
			var carryall = self.Trait<Carryall>();
			var body = self.Trait<BodyOrientation>();

			if (carryall.Carryable == null)
				return;

			var localOffset = carryall.CarryableOffset.Rotate(body.QuantizeOrientation(self.Orientation));
			var targetPosition = self.CenterPosition + body.LocalToWorld(localOffset);
			var targetLocation = self.World.Map.CellContaining(targetPosition);

			if (!self.World.Map.Contains(targetLocation))
				return;

			var droppableMobile = carryall.Carryable.Trait<Mobile>();

			if (!droppableMobile.CanEnterCell(targetLocation))
			{
				return;
			}

			// Put back into world
			self.World.AddFrameEndTask(w =>
			{
				if (self.IsDead)
					return;

				var cargo = carryall.Carryable;
				if (cargo == null)
					return;

				carryall.Carryable.Trait<IPositionable>().SetPosition(carryall.Carryable, targetLocation, SubCell.FullCell);
				carryall.Carryable.Trait<IFacing>().Facing = facing.Facing;

				var carryable = cargo.Trait<Carryable>();
				w.Add(cargo);
				carryall.DetachCarryable(self);
				carryable.UnReserve(cargo);
				carryable.Detached(cargo);
			});
		}

		void ITick.Tick(Actor self)
		{
			switch (State)
			{
				case OperationStateType.Harvest:
					HarvestTick(self);
					break;
				case OperationStateType.Drop:
					DropTick(self);
					break;
			}
		}

		// public void SetTarget(World w, WPos pos) { Operation.Target = Target.FromPos(pos); }
		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			Operation.CleanUp();
			OnRemovedFromWorld(self);
		}

		public override Activity GetAttackActivity(Actor self, AttackSource source, in Target newTarget,
			bool allowMove, bool forceAttack, Color? targetLineColor = null)
		{
			throw new NotImplementedException("AttackBomber requires vulture scripted Target");
		}
	}
}
