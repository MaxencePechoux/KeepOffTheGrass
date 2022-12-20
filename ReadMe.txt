 This is a Codingame.com challenge.
 As per requirements of the platform, all code must be in a single file.
 
 Below are my notes and thoughts on how to tackle this challenge.

 
 Questions:
 
 - What is the global strategy and how do we achieve it ?
    -> dominate with the number of tanks ?
    -> build lots of factories to max out spawning capacity ?
    -> divide in teams ? 
        => one attack team that eliminates opponents, and one team that colonizes ? Or one that moves forward and one that covers the back ?
!!!!!!!!=> maybe three teams: attack goes for the opponents tiles, defense tries to eradicate enemy tanks getting closer, and base team tries to cover all neutral tiles starting from our camp?

 - What is our 3 teams strategy (draft)?
    -> first, half our starting units go in base team and stay behind
        => each unit starts covering each neutral tile from the back to middle
    -> the other half goes into defense team and move straight for the middle area
    -> when defense team reaches middle area, we start spawning new units between attack and defense
        => we need to find the right balance between reinforcing defense numbers and start deploying attackers
    -> attack units start moving forward towards (furthest?) enemy occupied tiles
    -> defense team gets most of the spawned tanks. Two options:
        => scenario 1: we first spread across the vertical axis and try to cover each tile, to block enemies first, and then we reinforce each tile until we reach X units per tile
        => scenario 2: we first try to reinforce our units to X amount, to protect against strong enemy, and then we start spreading vertically
            - I think I prefer scenario 1, as scenario will leave us wide open for a long time
            - Furthermore, as soon as we have covered the vertical axis, we can start alternating (as new units cant move) between spawning and moving all units forward by one to improve score
    -> in parrallel to all that, we need to build factories:
        => either we play it safe and build the furthest back to make sure we dont block our path, but we lose potentially lots of territory
        => or more risky, we build in front of the defense to limit the tiles lost, in the risk of blocking ourselves if not spread well
        => or maybe we build behind the defense ? Then we can isolate part of our territory from the enemy at the risk of isolating ourselves if defense and attack teams lose
            -This needs testing but first option might be easier and preferable

 - Where do we move each tank ?
    -> if in 3 teams:
        => attack should go for enemy occupied tiles
        => defense: 
            - if not in middle zone yet, move towards middle and wait for enemy coming
            - if in middle and no enemy, wait
            - if enemy approaching and enough units, attack
        => base should move to closest neutral tile and proceed from left to right (or vice versa)
 
 - Where is the best location to spawn tank?
    -> if in teams: forward for attack, back-middle for coloniz ? on another tank's tile for attack, on a different one for coloniz?
    -> if 3 teams scenario:
        => spawn for defense first, once reached the middle
        => spawn for attack, from the front of the defense, once defense has reached middle
        => for now, we don't spawn for base team. To be adjusted if not sufficient
 
 - When is the best time to spawn a tank?
    -> any time we have more than 10 scrap ? (const here) : build of factory happens first, so if we want to build factory and have only 10, factory will be built
 
 - How many tanks should we spawn ?
    -> I think we shouldn't have an upper limit for the number of tanks, the more the better, at least until the computation for each movement is fast enough
    -> for the number of tanks spawned...maybe 1 each time for now

 - How to balance the teams (in 3 teams scenario) ?
    -> base team should probably be the smallest team. Maybe to start with half the original amount (2 so far) and keep it at that
    -> from start, prioritize defense team, while moving to center

 - Where is the best location to build a recycler?
    -> the scrap value should be worth it on this tile and around (create const for that threshold, add property on Tile for scrap sum ?)
    -> when a factory has finished recycling, we lose 5 tiles worth of score 
        => if we build too many in our camp, our score will go down fast!
        => the more forward the build, the better ?
 
 - When is the best time to build a recycler?
    -> whenever we have more than 10 scraps (const), we have a suitable tile (buildable and above threshold above), and we are below our target factories described below
 
 - How many recycler should we build ?
    -> if we have too many, we lose tiles; if we have too few we won't collect enough scrap to win
    -> several options:
        => have a const with a fixed value
            :simplest
        => have a const that is initialized at game start based on a certain parameter: probably the grid size-> the bigger the grid the higher number of recycler needed at any time
            :easy
        => have a variable updated each turn depending on certains factors...eg: if we have lots of scrap, we may not need as many factories, especially if we have a low score
            we also may not want too many factories from the start, or we ll get stuck; so maybe the variable should account for the turn number
            :hardest but smartest 
 
 - How do we prioritize between recycler build or tank build ? When should we save scraps ?
    -> I think we don't: if we want a factory, we build it, if we have enough left, we build 1 tank (for now)
    -> if we start building several factories/tanks per turn, maybe we ll need a strategy for saving scraps

Architecture:

 DONE- class GameInstance that handles game loop, read input and keep track of global variables
 DONE- static class Constants
 DONE- class Unit, contains the GetAction method, possess an AI of type IAI, instantiated in constructor when unit is being assigned to a team
 DONE- abstract class Team
 DONE- class AttackTeam : Team contains list of Unit
 DONE- class DefenseTeam : Team contains list of Unit
 DONE- class BaseTeam : Team contains list of Unit
 DONE- class Tile with all details provided by input
 DONE- global Map variable: 2 dimensional array of Tiles, with null for grass Tiles
 - global list of Neutral Tiles
 DONE- global list of my Tiles
 - global list of enemy Tiles
 DONE- const (for now) IdealFactories
 DONE- const ScrapsToBuild = 10
 DONE- const DesiredScrapsToBuild
 DONE- const DesiredScrapsToSpawn
 DONE- interface IAI, contains GetTarget
 DONE- class AIAttack : IAI
 DONE- class AIDefense : IAI
 DONE- class AIBase : IAI
 DONE- singleton class Logger, that will handle logging and output

Refactoring/Improvements TODO:

 DONE- Store commands for each turn into a StringBuilder (probably) and just append with ; at the end
 DONE- Create a GameInstance class instantiated in Main, to get out of static context
 DONE- Determine and store if we start from left side or right side: if any tank.X < Gridwidth / 2 -> we are on the left, otherwise we are on the right
 DONE- Keep track of the middle of X axis and of Defense line position/desired position?
 DONE- We might want to move CalculateTarget outside of the AI constructors...

Next Steps:

    DONE- Introduce persistence between turns: stop re instantiating everything each turn, and keep track of some data
    - Make defense team spread across column EVENLY
    DONE- Make attack team move towards enemy positions
    - Make attack team spawn at first from defense team positions
    DONE- Make base team cover the base
    - Determine each turn which unit goes to which team
    DONE- Determine position of recyclers more efficiently: closest from baseline as possible


Notes/ideas:
    Another possible strategy that seems to be used by top ranker is to shift from a team perspective to a phase perspective:
    - instead of having 3 teams, we would have three phases:
        -> Attack phase: units tush towards middle
        -> Defense phase: units spread vertically from middle to build a column of recycler to break the map in 2
        -> Covering base phase: once the map is broken, convert all neutral tiles in base
    - This could actually be achieved with my current 3 teams strategy too, with some alterations
    - The attack team could transform into one single scout unit
        -> the scout would rush far into enemy camp, and as soon as the defense team as finished breaking the map, we would reinforce to the max that scout into a super fortified unit, with all scrap available
        -> then while the base team is slowly covering all neutral tiles in camp, super fortified scout walks within enemy territory and drops a recycler every time possible to reduce enemy territory