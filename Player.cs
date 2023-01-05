using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 * Auto-generated code below aims at helping you parse
 * the standard input according to the problem statement.
 **/
class Player
{
    static void Main(string[] args)
    {
        var worldInstance = World.Instance;
        var logger = Logger.Instance;
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        worldInstance.MapWidth = int.Parse(inputs[0]);
        worldInstance.MapHeight = int.Parse(inputs[1]);

        worldInstance.GetWorldStates().SetState("shouldSpawnTank", 1);
        var tankList = new List<Tile>();

        //temp hack
        int init = 0;

        // game loop
        while (true)
        {
            worldInstance.Map = new Tile[worldInstance.MapWidth, worldInstance.MapHeight];
            inputs = Console.ReadLine().Split(' ');
            Helper.NewTurnCleanUp(worldInstance);
            worldInstance.GetWorldStates().SetState("matter", int.Parse(inputs[0]));
            int oppMatter = int.Parse(inputs[1]);
            for (int i = 0; i < worldInstance.MapHeight; i++)
            {
                for (int j = 0; j < worldInstance.MapWidth; j++)
                {
                    inputs = Console.ReadLine().Split(' ');
                    int scrapAmount = int.Parse(inputs[0]);
                    int owner = int.Parse(inputs[1]); // 1 = me, 0 = foe, -1 = neutral
                    int units = int.Parse(inputs[2]);
                    bool recycler = int.Parse(inputs[3]) == 1;
                    bool canBuild = int.Parse(inputs[4]) == 1;
                    bool canSpawn = int.Parse(inputs[5]) == 1;
                    bool inRangeOfRecycler = int.Parse(inputs[6]) == 1;

                    var tile = new Tile(j, i);
                    tile.IsAccessible = scrapAmount > 0 && !recycler;
                    worldInstance.Map[j, i] = tile;

                    if (owner == 1)
                    {
                        if (canBuild)
                        {
                            worldInstance.GetList("recyclerSpawnPoints").AddResource(new GameObject(tile));
                            worldInstance.GetWorldStates().ModifyState("RecyclerSpawnPoints", 1);
                        }

                        if (canSpawn)
                        {
                            worldInstance.GetList("tankSpawnPoints").AddResource(new GameObject(tile));
                            worldInstance.GetWorldStates().ModifyState("TankSpawnPoints", 1);
                        }

                        if (units > 0)
                        {
                            var tank = worldInstance.GetList("tanks").GetResource(tile) as Tank;
                            if (tank != null)
                            {
                                tank.ValueForCommand = units;
                            }
                            else
                            {
                                tankList.Add(tile);
                                worldInstance.GetList("tanks").AddResource(Helper.InitTank(units, tile));
                                worldInstance.GetWorldStates().ModifyState("Tanks", 1);
                            }
                        }

                        if (recycler)
                        {
                            worldInstance.GetList("recyclers").AddResource(new Recycler(tile));
                            worldInstance.GetWorldStates().ModifyState("Recyclers", 1);
                        }

                        //temp hack
                        if (init == 0)
                        {
                            init = j < World.Instance.MapWidth / 2 ? 1 : -1;
                        }
                    }
                    else if (owner == 0 && !recycler)
                    {

                        if (units > 0)
                        {

                            worldInstance.GetList("enemyTankTiles").AddResource(new EnemyTankTile(tile, units));
                            worldInstance.GetWorldStates().ModifyState("EnemyTankTiles", 1);
                        }
                        else
                        {
                            worldInstance.GetList("enemyTiles").AddResource(new EnemyTile(tile));
                            worldInstance.GetWorldStates().ModifyState("EnemyTiles", 1);
                        }
                    }
                    else
                    {
                        if (scrapAmount > 0)
                        {
                            worldInstance.GetList("scrapTiles").AddResource(new ScrapTile(tile, scrapAmount));
                            worldInstance.GetWorldStates().ModifyState("ScrapTiles", 1);
                        }
                    }
                }
            }

            if (worldInstance.GetWorldStates().GetState("Recyclers") < 3)
            {
                worldInstance.GetWorldStates().SetState("shouldSpawnRecycler", 1);
            }
            else
            {
                worldInstance.GetWorldStates().RemoveState("shouldSpawnRecycler");
            }

            Helper.ReconciliateTanks(tankList);

            // Write an action using Console.WriteLine()
            // To debug: Console.Error.WriteLine("Debug messages...");

            //Logger.LogWorldStates();

            //Logger.PrintQueue("enemyTankTiles");

            var tankSpawner = Helper.InitTankSpawner(worldInstance.GetWorldStates().GetState("matter") / 20);
            var recyclerSpawner = Helper.InitRecyclerSpawner();
            recyclerSpawner.Execute();
            tankSpawner.Execute();

            foreach (Tank tank in worldInstance.GetList("tanks").list)
            {
                tank.Execute();
            }

            //temp hack
            if (init == -1)
            {
                Console.WriteLine("WAIT;");
            }
            else
            {
                logger.PublishOutput();
            }
        }
    }

    
}

#region GOAP

#region WorldStates

public class WorldState
{
    public string key;
    public int value;
}

public class WorldStates
{
    public Dictionary<string, int> States { get; }

    public WorldStates()
    {
        States = new Dictionary<string, int>();
    }

    public int GetState(string key)
    {
        return HasState(key) ? States[key] : 0;
    }

    public bool HasState(string key)
    {
        return States.ContainsKey(key);
    }

    public void AddState(string key, int value)
    {
        States.Add(key, value);
    }

    public void ModifyState(string key, int value)
    {
        if (States.ContainsKey(key))
        {
            States[key] += value;
            if (States[key] <= 0)
            {
                RemoveState(key);
            }
        }
        else
        {
            AddState(key, value);
        }
    }

    public void RemoveState(string key)
    {
        if (States.ContainsKey(key))
        {
            States.Remove(key);
        }
    }

    public void SetState(string key, int value)
    {
        if (States.ContainsKey(key))
        {
            States[key] = value;
        }
        else
        {
            States.Add(key, value);
        }
    }
}

#endregion

#region World

public class ResourceList
{
    public List<GameObject> list = new List<GameObject>();
    public string tag;
    public string modState;

    public ResourceList()
    {
    }

    public void AddResource(GameObject r)
    {
        list.Add(r);
    }

    public GameObject RemoveResource()
    {
        if (list.Count == 0) return null;

        var resource = list.First();

        list.Remove(resource);

        return resource;
    }

    public void RemoveResource(GameObject r)
    {
        list.Remove(r);
    }

    public void ClearList()
    {
        list.Clear();
    }

    public GameObject GetResource(Tile tile)
    {
        foreach (var r in list)
        {
            if (r.Tile.Equals(tile))
            {
                return r;
            }
        }
        return null;
    }
}

public sealed class World
{
    private static readonly World instance = new World();
    private static WorldStates worldStates;
    private static ResourceList recyclers;
    private static ResourceList tanks;
    private static ResourceList enemyTankTiles;
    private static ResourceList enemyTiles;
    private static ResourceList scrapTiles;
    private static ResourceList tankTargets;
    private static ResourceList recyclerSpawnPoints;
    private static ResourceList tankSpawnPoints;
    private static Dictionary<string, ResourceList> resources = new Dictionary<string, ResourceList>();

    static World()
    {
        worldStates = new WorldStates();
        recyclers = new ResourceList();
        tanks = new ResourceList();
        enemyTankTiles = new ResourceList();
        enemyTiles = new ResourceList();
        scrapTiles = new ResourceList();
        tankTargets = new ResourceList();
        recyclerSpawnPoints = new ResourceList();
        tankSpawnPoints = new ResourceList();
        resources.Add("recyclers", recyclers);
        resources.Add("tanks", tanks);
        resources.Add("enemyTankTiles", enemyTankTiles);
        resources.Add("enemyTiles", enemyTiles);
        resources.Add("scrapTiles", scrapTiles);
        resources.Add("tankTargets", tankTargets);
        resources.Add("recyclerSpawnPoints", recyclerSpawnPoints);
        resources.Add("tankSpawnPoints", tankSpawnPoints);
    }

    public ResourceList GetList(string type)
    {
        return resources[type];
    }

    public void ClearList(string type)
    {
        resources[type].ClearList();
    }

    private World()
    {
    }

    public static World Instance => instance;

    public WorldStates GetWorldStates()
    {
        return worldStates;
    }

    public Tile[,] Map;
    public int MapWidth;
    public int MapHeight;
}

#endregion

#region Planner

public class Node
{
    public Node parent;
    public float cost;
    public Dictionary<string, int> state;
    public Action action;

    public Node(Node parent, float cost, Dictionary<string, int> allStates, Action action)
    {
        this.parent = parent;
        this.cost = cost;
        this.state = new Dictionary<string, int>(allStates);
        this.action = action;
    }

    public Node(Node parent, float cost, Dictionary<string, int> allStates, Dictionary<string, int> beliefStates, Action action)
    {
        this.parent = parent;
        this.cost = cost;
        this.state = new Dictionary<string, int>(allStates);
        this.action = action;

        foreach (KeyValuePair<string, int> b in beliefStates)
        {
            if (!this.state.ContainsKey(b.Key))
            {
                this.state.Add(b.Key, b.Value);
            }
        }

    }
}

public class Planner
{
    public Queue<Action> Plan(List<Action> actions, Dictionary<string, int> goal, WorldStates beliefStates)
    {
        List<Action> usableActions = new List<Action>();

        foreach (Action action in actions)
        {
            if (action.IsAchievable())
            {
                usableActions.Add(action);
            }
        }

        List<Node> leaves = new List<Node>();
        Node start = new Node(null, 0, World.Instance.GetWorldStates().States, null);

        bool success = BuildGraph(start, leaves, usableActions, goal);

        if (!success)
        {
            return null;
        }

        Node cheapest = null;

        foreach (Node leaf in leaves)
        {
            if (cheapest == null)
            {
                cheapest = leaf;
            }
            else
            {
                if (leaf.cost < cheapest.cost)
                {
                    cheapest = leaf;
                }
            }
        }

        List<Action> result = new List<Action>();

        Node n = cheapest;

        while (n != null)
        {
            if (n.action != null)
            {
                result.Insert(0, n.action);
            }
            n = n.parent;
        }

        Queue<Action> queue = new Queue<Action>();

        foreach (Action a in result)
        {
            queue.Enqueue(a);
        }

        return queue;
    }

    bool BuildGraph(Node parent, List<Node> leaves, List<Action> usableActions, Dictionary<string, int> goal)
    {
        bool foundPath = false;

        foreach (Action action in usableActions)
        {
            //Logger.LogDebugMessage("Checking action " + action.actionName);
            if (action.IsAchievableGiven(parent.state))
            {
                //Logger.LogDebugMessage("it is achievable!");

                Dictionary<string, int> currentState = new Dictionary<string, int>(parent.state);

                foreach (KeyValuePair<string, int> effect in action.afterEffects.States)
                {
                    if (!currentState.ContainsKey(effect.Key))
                    {
                        currentState.Add(effect.Key, effect.Value);
                    }
                }

                Node node = new Node(parent, parent.cost + action.cost, currentState, action);

                if (GoalAchieved(goal, currentState))
                {
                    leaves.Add(node);
                    foundPath = true;
                }
                else
                {
                    List<Action> subset = ActionSubset(usableActions, action);
                    bool found = BuildGraph(node, leaves, subset, goal);

                    if (found)
                    {
                        foundPath = true;
                    }
                }
            }
        }
        return foundPath;
    }

    List<Action> ActionSubset(List<Action> usableActions, Action removeMe)
    {
        List<Action> subset = new List<Action>();

        foreach (Action a in usableActions)
        {
            if (!a.Equals(removeMe))
            {
                subset.Add(a);
            }
        }
        return subset;
    }

    bool GoalAchieved(Dictionary<string, int> goal, Dictionary<string, int> currentState)
    {
        foreach (KeyValuePair<string, int> g in goal)
        {
            if (!currentState.ContainsKey(g.Key))
            {
                return false;
            }
        }
        return true;
    }
}

#endregion

#region Inventory

public class Inventory
{
    List<GameObject> items = new List<GameObject>();

    public void AddItem(GameObject i)
    {
        items.Add(i);
    }

    public GameObject FindItemWithTag(string tag)
    {
        foreach (GameObject i in items)
        {
            if (i.Tag == tag)
            {
                return i;
            }
        }
        return null;
    }

    public void RemoveItem(GameObject i)
    {
        int index = -1;

        foreach (GameObject item in items)
        {
            index++;
            if (item == i)
            {
                break;
            }
        }

        if (index >= -1)
        {
            items.RemoveAt(index);
        }
    }
}

#endregion

#endregion

#region Actions

#region Action

public abstract class Action
{
    public string actionName = "Action";
    public float cost = 1.0f;
    public GameObject target;
    public string targetTag;
    public float duration = 0f;
    public WorldStates preConditions;
    public WorldStates afterEffects;
    public WorldStates agentBeliefs;

    public bool running = false;

    public Action(GameObject target, string targetTag, WorldStates preConditions, WorldStates afterEffects, WorldStates agentBeliefs)
    {
        this.target = target;
        this.targetTag = targetTag;
        this.preConditions = preConditions;
        this.afterEffects = afterEffects;
        this.agentBeliefs = agentBeliefs;
    }

    public bool IsAchievable()
    {
        return true;
    }

    public bool IsAchievableGiven(Dictionary<string, int> conditions)
    {
        //Logger.LogDebugMessage("IsAchievableGiven: " + actionName);
        foreach (var key in conditions)
        {
            //Logger.LogDebugMessage("IsAchievableGiven: param key " + key);
        }
        foreach (var key in preConditions.States)
        {
            //Logger.LogDebugMessage("IsAchievableGiven: preConditions key " + key);
        }

        foreach (KeyValuePair<string, int> p in preConditions.States)
        {
            if (!conditions.ContainsKey(p.Key))
            {
                return false;
            }
        }
        return true;
    }


    public abstract bool PrePerform();
    public abstract bool PostPerform();
    public abstract string GetCommand(Tile agentTile, int value);
}

#endregion

#region AttackEnemy

public class AttackEnemy : Action
{
    int power;
    public AttackEnemy(GameObject target, string targetTag, WorldStates preConditions, WorldStates afterEffects, WorldStates agentBeliefs, int power)
        : base(target, targetTag, preConditions, afterEffects, agentBeliefs)
    {
        actionName = "AttackEnemy";
        this.power = power;
    }

    public override bool PrePerform()
    {
        target = World.Instance.GetList("enemyTankTiles").RemoveResource();
        if (target == null)
        {
            Logger.LogDebugMessage("No more enemy target available");
            return false;
        }

        if (power < ((EnemyTankTile)target).Strength)
        {
            Logger.LogDebugMessage("Enemy too strong! My power: " + power + ", Enemy power: " + ((EnemyTankTile)target).Strength + " at " + target.Tile);

            World.Instance.GetList("enemyTankTiles").AddResource(target);
            target = null;
            return false;
        }

        return true;
    }

    public override bool PostPerform()
    {
        World.Instance.GetWorldStates().ModifyState("EnemyTankTiles", -1);
        return true;
    }

    public override string GetCommand(Tile agentTile, int value)
    {
        if (target == null)
        {
            return "";
        }
        else
        {
            return "MOVE " + value + " " + agentTile.X + " " + agentTile.Y + " " + target.Tile.X + " " + target.Tile.Y;
        }
    }
}

#endregion

#region MoveToEnemyTile

public class MoveToEnemyTile : Action
{
    public MoveToEnemyTile(GameObject target, string targetTag, WorldStates preConditions, WorldStates afterEffects, WorldStates agentBeliefs)
        : base(target, targetTag, preConditions, afterEffects, agentBeliefs)
    {
        actionName = "MoveToEnemyTile";
    }

    public override bool PrePerform()
    {
        target = World.Instance.GetList("enemyTiles").RemoveResource();
        if (target == null)
        {
            return false;
        }
        return true;
    }

    public override bool PostPerform()
    {
        World.Instance.GetWorldStates().ModifyState("EnemyTiles", -1);
        return true;
    }

    public override string GetCommand(Tile agentTile, int value)
    {
        if (target == null)
        {
            return "";
        }
        else
        {
            return "MOVE " + value + " " + agentTile.X + " " + agentTile.Y + " " + target.Tile.X + " " + target.Tile.Y;
        }
    }
}

#endregion

#region MoveToScrapTile

public class MoveToScrapTile : Action
{
    public MoveToScrapTile(GameObject target, string targetTag, WorldStates preConditions, WorldStates afterEffects, WorldStates agentBeliefs)
        : base(target, targetTag, preConditions, afterEffects, agentBeliefs)
    {
        actionName = "MoveToScrapTile";
    }

    public override bool PrePerform()
    {
        target = World.Instance.GetList("scrapTiles").RemoveResource();
        if (target == null)
        {
            return false;
        }
        return true;
    }

    public override bool PostPerform()
    {
        World.Instance.GetWorldStates().ModifyState("ScrapTiles", -1);
        return true;
    }

    public override string GetCommand(Tile agentTile, int value)
    {
        if (target == null)
        {
            return "";
        }
        else
        {
            return "MOVE " + value + " " + agentTile.X + " " + agentTile.Y + " " + target.Tile.X + " " + target.Tile.Y;
        }
    }
}

#endregion

#region SpawnRecycler

public class SpawnRecycler : Action
{
    public SpawnRecycler(GameObject target, string targetTag, WorldStates preConditions, WorldStates afterEffects, WorldStates agentBeliefs)
        : base(target, targetTag, preConditions, afterEffects, agentBeliefs)
    {
        actionName = "SpawnRecycler";
    }

    public override bool PrePerform()
    {
        var matter = World.Instance.GetWorldStates().GetState("matter");
        if (matter >= 10)
        {
            target = World.Instance.GetList("recyclerSpawnPoints").RemoveResource();
            if (target == null)
            {
                return false;
            }
            return true;
        }
        return false;
    }

    public override bool PostPerform()
    {
        World.Instance.GetWorldStates().ModifyState("matter", -10);
        World.Instance.GetWorldStates().ModifyState("RecyclerSpawnPoints", -1);
        return true;
    }

    public override string GetCommand(Tile agentTile, int value)
    {
        if (target == null)
        {
            return "";
        }
        else
        {
            return "BUILD " + target.Tile.X + " " + target.Tile.Y;
        }
    }
}

#endregion

#region SpawnTank

public class SpawnTank : Action
{
    int qty;
    public SpawnTank(GameObject target, string targetTag, WorldStates preConditions, WorldStates afterEffects, WorldStates agentBeliefs, int qty)
        : base(target, targetTag, preConditions, afterEffects, agentBeliefs)
    {
        actionName = "SpawnTank";
        this.qty = qty;
    }

    public override bool PrePerform()
    {
        var matter = World.Instance.GetWorldStates().GetState("matter");
        if (matter >= 10*qty)
        {
            target = World.Instance.GetList("tankSpawnPoints").RemoveResource();
            if (target == null)
            {
                return false;
            }
            return true;
        }
        return false;
    }

    public override bool PostPerform()
    {
        World.Instance.GetWorldStates().ModifyState("matter", -10*qty);
        World.Instance.GetWorldStates().ModifyState("TankSpawnPoints", -1);

        return true;
    }

    public override string GetCommand(Tile agentTile, int value)
    {
        if (target == null)
        {
            return "";
        }
        else
        {
            return "SPAWN " + qty + " " + target.Tile.X + " " + target.Tile.Y;
        }
    }
}

#endregion

#endregion

#region Agents

#region Agent

public class SubGoal
{
    public Dictionary<string, int> sgoals;
    public bool remove;

    public SubGoal(string s, int i, bool r)
    {
        sgoals = new Dictionary<string, int>();
        sgoals.Add(s, i);
        remove = r;
    }

    public override string ToString()
    {
        return "Goal: " + sgoals.First().Key;
    }
}

public class Agent : GameObject
{
    public List<Action> actions = new List<Action>();
    public Dictionary<SubGoal, int> goals = new Dictionary<SubGoal, int>();
    public WorldStates beliefs = new WorldStates();

    Planner planner;
    Queue<Action> actionQueue;
    public Action currentAction;
    SubGoal currentGoal;

    public int ValueForCommand = 0;

    public Agent(List<Action> actions, Tile tile) : base(tile)
    {
        this.actions = new List<Action>(actions);
    }

    public void Execute()
    {
        //Logger.LogDebugMessage("Executing for " + Tag + " on tile " + Tile);
        if (planner == null || actionQueue == null)
        {
            planner = new Planner();

            var sortedGoals = goals.OrderByDescending(e => e.Value).Select(e => e.Key);

            foreach (SubGoal sg in sortedGoals)
            {
                Logger.LogDebugMessage(Tile + "Planning for " + sg);

                actionQueue = planner.Plan(actions, sg.sgoals, beliefs);
                if (actionQueue != null)
                {
                    Logger.LogDebugMessage("Queue built!");

                    currentGoal = sg;
                    break;
                }
            }
        }

        if (actionQueue != null && actionQueue.Count == 0)
        {
            //Logger.LogDebugMessage("Planner is not null");

            if (currentGoal.remove)
            {
                goals.Remove(currentGoal);
            }
            planner = null;
        }

        if (actionQueue != null && actionQueue.Count > 0)
        {
            //Logger.LogDebugMessage("Planner is not null");

            currentAction = actionQueue.Dequeue();

            if (currentAction.PrePerform())
            {
                string cmd = currentAction.GetCommand(Tile, ValueForCommand);
                Logger.LogDebugMessage("Logging Action: " + cmd);
                Logger.Instance.LogAction(cmd);
                currentAction.PostPerform();
            }
            else
            {
                actionQueue = null;
            }
        }
        //Logger.LogDebugMessage("Leaving execute");
    }
}

#endregion

#region Tank

public class Tank : Agent
{
    public Tank(List<Action> actions, Tile tile, int strength) : base(actions, tile)
    {
        SubGoal s1 = new SubGoal("targetEliminated", 1, false);
        SubGoal s2 = new SubGoal("tileConquered", 1, false);

        goals.Add(s1, 2);
        goals.Add(s2, 1);
        ValueForCommand = strength;
        Tag = "Tank";
    }
}

#endregion

#region Spawner

public class Spawner : Agent
{
    public Spawner(List<Action> actions, Tile tile) : base(actions, tile)
    {
        SubGoal s1 = new SubGoal("recyclerSpawned", 1, false);
        SubGoal s2 = new SubGoal("tankSpawned", 1, false);

        goals.Add(s1, 2);
        goals.Add(s2, 1);
        ValueForCommand = 1;
        Tag = "Spawner";
    }
}

#endregion

#endregion

#region Resources

#region GameObject

public class GameObject
{
    public Tile Tile;
    public GameObject(Tile tile)
    {
        Tile = tile;
    }
    public string Tag;

    public override bool Equals(Object obj)
    {
        GameObject gobj = obj as GameObject;
        if (gobj == null)
        {
            return false;
        }
        else
        {
            return Tile.Equals(gobj.Tile);
        }
    }

    public override int GetHashCode()
    {
        return this.Tile.GetHashCode();
    }
}

#endregion

#region Recycler

public class Recycler : GameObject
{
    public Recycler(Tile tile) : base(tile)
    {
        Tag = "Recycler";
    }
}

#endregion

#region EnemyTankTile

public class EnemyTankTile : GameObject
{
    public int Strength;
    public EnemyTankTile(Tile tile, int strength) : base(tile)
    {
        Tag = "EnemyTankTile";
        this.Strength = strength;
    }
}

#endregion

#region EnemyTile

public class EnemyTile : GameObject
{
    public EnemyTile(Tile tile) : base(tile)
    {
        Tag = "EnemyTile";
    }
}

#endregion

#region ScrapTile

public class ScrapTile : GameObject
{
    public int Scrap;
    public ScrapTile(Tile tile, int scrap) : base(tile)
    {
        Tag = "ScrapTile";
        Scrap = scrap;
    }
}

#endregion

#region Tile

public class Tile
{
    public Tile(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Tile()
    {
    }

    public int X;
    public int Y;
    public int Cost;
    public int Distance;
    public int CostDistance => Cost + Distance;
    public bool IsAccessible;
    public Tile Parent;

    public void SetDistance(int targetX, int targetY)
    {
        this.Distance = Math.Abs(targetX - X) + Math.Abs(targetY - Y);
    }

    public override string ToString()
    {
        return X + " " + Y;
    }

    public override bool Equals(Object obj)
    {
        Tile tile = obj as Tile;
        if (tile == null)
        {
            return false;
        }
        else
        {
            return X == tile.X && Y == tile.Y;
        }
    }

    public override int GetHashCode()
    {
        return this.X.GetHashCode() + this.Y.GetHashCode();
    }
}

#endregion

#endregion

#region Helper

public static class Helper
{
    public static Spawner InitRecyclerSpawner()
    {
        var preConditionsSR = new WorldStates();
        preConditionsSR.SetState("shouldSpawnRecycler", 1);
        var afterEffectsSR = new WorldStates();
        afterEffectsSR.SetState("recyclerSpawned", 1);

        var spawnRecyclerAction = new SpawnRecycler(null, "", preConditionsSR, afterEffectsSR, null);

        return new Spawner(new List<Action> { spawnRecyclerAction }, new Tile(0, 0));
    }

    public static Spawner InitTankSpawner(int qty)
    {
        var preConditionsST = new WorldStates();
        preConditionsST.SetState("shouldSpawnTank", 1);
        var afterEffectsST = new WorldStates();
        afterEffectsST.SetState("tankSpawned", 1);

        var spawnTankAction = new SpawnTank(null, "", preConditionsST, afterEffectsST, null, qty);

        return new Spawner(new List<Action> { spawnTankAction }, new Tile(0, 0));
    }

    public static Tank InitTank(int strength, Tile tile)
    {
        var preConditionsA = new WorldStates();
        preConditionsA.SetState("EnemyTankTiles", 1);
        var afterEffectsA = new WorldStates();
        afterEffectsA.SetState("targetEliminated", 1);

        var attackEnemyTileAction = new AttackEnemy(null, "", preConditionsA, afterEffectsA, null, strength);

        var preConditionsET = new WorldStates();
        //preConditionsET.SetState("shouldSpawnTank", 1);
        var afterEffectsET = new WorldStates();
        afterEffectsET.SetState("tileConquered", 1);

        var moveToEnemyTileAction = new MoveToEnemyTile(null, "", preConditionsET, afterEffectsET, null);

        var preConditionsST = new WorldStates();
        //preConditionsST.SetState("shouldSpawnTank", 1);
        var afterEffectsST = new WorldStates();
        afterEffectsST.SetState("tileConquered", 1);

        var moveToScrapTileAction = new MoveToScrapTile(null, "", preConditionsST, afterEffectsST, null);

        return new Tank(new List<Action> { attackEnemyTileAction, moveToEnemyTileAction, moveToScrapTileAction }, tile, strength);
    }

    public static void NewTurnCleanUp(World worldInstance)
    {
        worldInstance.ClearList("recyclerSpawnPoints");
        worldInstance.GetWorldStates().RemoveState("RecyclerSpawnPoints");
        worldInstance.ClearList("tankSpawnPoints");
        worldInstance.GetWorldStates().RemoveState("TankSpawnPoints");
        worldInstance.ClearList("enemyTiles");
        worldInstance.GetWorldStates().RemoveState("EnemyTiles");
        worldInstance.ClearList("enemyTankTiles");
        worldInstance.GetWorldStates().RemoveState("EnemyTankTiles");
        worldInstance.ClearList("recyclers");
        worldInstance.GetWorldStates().RemoveState("Recyclers");
        worldInstance.ClearList("scrapTiles");
        worldInstance.GetWorldStates().RemoveState("ScrapTiles");
    }

    static List<Tile> GetWalkableTiles(Tile[,] map, Tile currentTile, Tile targetTile)
    {
        var possibleTiles = new List<Tile>()
        {
            new Tile { X = currentTile.X, Y = currentTile.Y - 1, Parent = currentTile, Cost = currentTile.Cost + 1 },
            new Tile { X = currentTile.X, Y = currentTile.Y + 1, Parent = currentTile, Cost = currentTile.Cost + 1 },
            new Tile { X = currentTile.X - 1, Y = currentTile.Y, Parent = currentTile, Cost = currentTile.Cost + 1 },
            new Tile { X = currentTile.X + 1, Y = currentTile.Y, Parent = currentTile, Cost = currentTile.Cost + 1 },
        };

        possibleTiles.ForEach(tile => tile.SetDistance(targetTile.X, targetTile.Y));

        var maxX = World.Instance.MapWidth - 1;
        var maxY = World.Instance.MapHeight - 1;

        return possibleTiles
                .Where(tile => tile.X >= 0 && tile.X <= maxX)
                .Where(tile => tile.Y >= 0 && tile.Y <= maxY)
                .Where(tile => map[tile.Y,tile.X].IsAccessible)
                .ToList();
    }

    public static Tile FindNextTileToTarget(Tile[,] map, Tile start, Tile finish)
    {
        var visitedTiles = new List<Tile>();
        var activeTiles = new List<Tile>();
        activeTiles.Add(start);
        while (activeTiles.Any())
        {
            var checkTile = activeTiles.OrderBy(x => x.CostDistance).First();

            if (checkTile.X == finish.X && checkTile.Y == finish.Y)
            {
                while (checkTile.Parent != null)
                {
                    checkTile = checkTile.Parent;
                }
                return checkTile;
            }

            visitedTiles.Add(checkTile);
            activeTiles.Remove(checkTile);

            var walkableTiles = GetWalkableTiles(map, checkTile, finish);

            foreach (var walkableTile in walkableTiles)
            {
                if (visitedTiles.Any(x => x.X == walkableTile.X && x.Y == walkableTile.Y))
                    continue;

                if (activeTiles.Any(x => x.X == walkableTile.X && x.Y == walkableTile.Y))
                {
                    var existingTile = activeTiles.First(x => x.X == walkableTile.X && x.Y == walkableTile.Y);
                    if (existingTile.CostDistance > checkTile.CostDistance)
                    {
                        activeTiles.Remove(existingTile);
                        activeTiles.Add(walkableTile);
                    }
                }
                else
                {
                    activeTiles.Add(walkableTile);
                }
            }
        }

        return null;
    }

    public static void ReconciliateTanks(List<Tile> tankList)
    {
        foreach (var tank in World.Instance.GetList("tanks").list)
        {
            if (!tankList.Contains(tank.Tile))
            {
                World.Instance.GetList("tanks").RemoveResource(tank);
                World.Instance.GetWorldStates().ModifyState("Tanks", -1);
            }
        }
    }
}

#endregion

#region Logger

public sealed class Logger
{
    StringBuilder Output;
    private static Logger instance;

    private Logger()
    {
        Output = new StringBuilder();
    }

    public static Logger Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new Logger();
            }
            return instance;
        }
    }

    public void LogAction(string action)
    {
        Output.Append(action + ";");
    }

    public void LogUIMessage(string message)
    {
        Output.Append("MESSAGE " + message + ";");
    }

    public static void LogDebugMessage(string message)
    {
        Console.Error.WriteLine(message);
    }

    public void PublishOutput()
    {
        var result = Output.ToString();

        if (result != "")
        {
            Console.WriteLine(result);
        }
        else
        {
            Console.WriteLine("WAIT;");
        }
        Output.Clear();
    }

    public static void LogWorldStates()
    {
        foreach (var state in World.Instance.GetWorldStates().States)
        {
            Console.Error.WriteLine("State: " + state.Key + " " + state.Value);
        }
    }

    public static void PrintQueue(string type)
    {
        Console.Error.WriteLine("Queue: " + type);
        foreach (var gobj in World.Instance.GetList(type).list)
        {
            Console.Error.WriteLine("Tile: " + gobj.Tile + ", tag: " + gobj.Tag);
        }
    }

}

#endregion