--[[
   Copyright (c) The OpenRA Developers and Contributors
   This file is part of OpenRA, which is free software. It is made
   available to you under the terms of the GNU General Public License
   as published by the Free Software Foundation, either version 3 of
   the License, or (at your option) any later version. For more
   information, see COPYING.
]]

Difficulty = Map.LobbyOptionOrDefault("difficulty", "normal")

Mentat = UserInterface.Translate("mentat")


SendMonumentReinforcements = function(
			player, delay, pathFunction,
			unitTypes, customCondition, customHuntFunction
	)
	Trigger.AfterDelay(delay, function()
		if customCondition and customCondition() then
			return
		end

		local path = pathFunction()
		local units = Reinforcements.ReinforceWithTransport(player, "carryall.reinforce", unitTypes, path, { path[1] })[2]

		if not customHuntFunction then
			customHuntFunction = IdleHunt
		end
		Utils.Do(units, customHuntFunction)

		SendMonumentReinforcements(player, delay, pathFunction, unitTypes, customCondition, customHuntFunction)
	end)
end
