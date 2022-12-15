using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;


public class Player
{
    static void Main(string[] args)
    {
        GameInstance.Instance.Start();
    }
}

public static class Constants
{
    public const int IDEAL_RECYCLERS = 5;
    public const int MIN_MATTER_TO_BUILD = 30;
    public const int MIN_MATTER_TO_SPAWN = 10;
    public const int MIN_SCRAPPABLE_TO_BUILD = 20;
    public const int NEUTRAL = -1;
    public const int ME = 1;
    public const int ENEMY = 0;
    public const string MOVE = "MOVE";
    public const string BUILD = "BUILD";
    public const string SPAWN = "SPAWN";
    public const string WAIT = "WAIT";
    public const string MESSAGE = "MESSAGE";
}

public enum CampPosition
{
    RIGHT = -1,
    INIT = 0,
    LEFT = 1
}

public enum UnitTeam
{
    ATTACK,
    DEFENSE,
    BASE
}

public sealed class GameInstance
{
    private static GameInstance instance;
    Logger logger = Logger.Instance;

    private GameInstance()
    {
    }

    public static GameInstance Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameInstance();
            }
            return instance;
        }
    }

    public Tile[,] Map { get; private set; }

    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    public int MyMatter { get; private set; }
    public CampPosition Direction { get; private set; }
    public int DefenseLine { get; private set; }
    public UnitFactory UnitFactory { get; private set; }
    public List<Tile> myUnitsTiles;
    public TeamManager teamManager;
    public RecyclerFactory recyclerFactory;


    public void Start()
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        MapWidth = int.Parse(inputs[0]);
        MapHeight = int.Parse(inputs[1]);
        Direction = CampPosition.INIT;
        DefenseLine = MapWidth / 2;

        // game loop
        while (true)
        {
            inputs = Console.ReadLine().Split(' ');
            MyMatter = int.Parse(inputs[0]);
            int oppMatter = int.Parse(inputs[1]);
            Map = new Tile[MapWidth, MapHeight];
            myUnitsTiles = new List<Tile>();
            teamManager = new TeamManager();
            recyclerFactory = new RecyclerFactory();
            UnitFactory = new UnitFactory();

            for (int i = 0; i < MapHeight; i++)
            {
                for (int j = 0; j < MapWidth; j++)
                {
                    inputs = Console.ReadLine().Split(' ');
                    var scrapAmount = int.Parse(inputs[0]);
                    if (scrapAmount > 0)
                    {
                        var tile = new Tile(j, i,
                            scrapAmount,
                            int.Parse(inputs[1]),
                            int.Parse(inputs[2]),
                            int.Parse(inputs[3]) == 1,
                            int.Parse(inputs[4]) == 1,
                            int.Parse(inputs[5]) == 1,
                            int.Parse(inputs[6]) == 1);
                        Map[j, i] = tile;

                        if (tile.Owner == Constants.ME)
                        {
                            if (tile.Units > 0)
                            {
                                myUnitsTiles.Add(tile);
                            }

                            if (tile.Recycler)
                            {
                                recyclerFactory.AddRecycler(tile);
                            }

                            if (tile.CanBuild)
                            {
                                recyclerFactory.AddBuildableTile(tile);
                            }
                        }
                    }
                }
            }

            if (Direction == CampPosition.INIT)
            {
                Direction = myUnitsTiles[0].X < MapWidth / 2 ? CampPosition.LEFT : CampPosition.RIGHT;
            }

            recyclerFactory.BuildRecyclersIfNeeded();

            teamManager.AssignMembersToTeams(myUnitsTiles);
            teamManager.ManageTeamsActions();

            UnitFactory.SpawnUnits();

            logger.PublishOutput();
        }
    }
}

public static class Helper
{
    public static void TestRandomScrappableAmounts(Logger logger, Tile[,] map, int mapWidth, int mapHeight, int nbOfTests)
    {
        var rnd = new Random();
        var i = 0;
        while (i < nbOfTests)
        {
            var x = rnd.Next(mapWidth);
            var y = rnd.Next(mapHeight);
            if (map[x, y] != null)
            {
                logger.LogDebugMessage("Tile [" + x + "," + y + "]: Total: " + map[x, y].TotalScrappableAmount);
                i++;
            }
        }
    }

    public static void LogTeamCompositions(Logger logger, TeamManager teamManager)
    {
        foreach (var unit in teamManager.FullTeam)
        {
            logger.LogDebugMessage(unit.ToString());
        }
    }
}

public interface IAI
{
    string GetAction();
    bool IsInPosition();
    void CalculateTarget();
}

public abstract class AI : IAI
{
    public string GetAction()
    {
        return Action.Move(1, Location.X, Location.Y, Target.X, Target.Y);
    }

    public virtual bool IsInPosition()
    {
        return false;
    }

    public abstract void CalculateTarget();

    protected Tile Location { get; set; }
    protected Tile Target { get; set; }
    protected GameInstance GameInstance = GameInstance.Instance;
}

public class AIAttack : AI
{
    public AIAttack(Tile tile)
    {
        Location = tile;
        CalculateTarget();
    }

    public override void CalculateTarget()
    { }
}

public class AIDefense : AI
{
    public AIDefense(Tile tile)
    {
        Location = tile;
        CalculateTarget();
    }
    
    public override bool IsInPosition()
    {
        return Location.X == GameInstance.DefenseLine;
    }

    public override void CalculateTarget()
    {
        int x = GameInstance.DefenseLine;
        int y = Location.Y;
        var targetFound = GameInstance.Map[x, y];
        Target = targetFound != null ? targetFound : FindClosestNonGrassTile(x, y);

    }

    Tile FindClosestNonGrassTile(int x, int y)
    {
        Tile tile = null;
        var direction = y > GameInstance.MapHeight / 2 ? 1 : -1;
        var goal = y > GameInstance.MapHeight / 2 ? GameInstance.MapHeight - 1 : 0;
        var n = y;
        var i = x;

        while (i != Location.X)
        {
            while (n != goal)
            {
                n += direction;
                tile = GameInstance.Map[i, n];

                if (tile != null)
                {
                    return tile;
                }
            }

            direction *= -1;
            goal = goal == 0 ? GameInstance.MapHeight - 1 : 0;
            n = y;

            while (n != goal)
            {
                n += direction;
                tile = GameInstance.Map[i, n];

                if (tile != null)
                {
                    return tile;
                }
            }

            //If we cannot find a suitable tile in the middle, we try one column closer to the unit
            i -= (int) GameInstance.Direction;
        }

        return Location;
    }
}

public class AIBase : AI
{
    public AIBase(Tile tile)
    {
        Location = tile;
        CalculateTarget();
    }

    public override void CalculateTarget()
    {
        if (GameInstance.Direction == CampPosition.LEFT)
        {
            Target = GameInstance.Map[0, 0];
        }
        else
        {
            Target = GameInstance.Map[GameInstance.MapWidth - 1, 0];
        }
    }
}

public class Unit
{
    public Tile Tile { get; }
    public UnitTeam Team { get; }
    IAI ai;

    public Unit(UnitTeam team, Tile tile)
    {
        Tile = tile;

        switch (team)
        {
            case UnitTeam.ATTACK:
                Team = UnitTeam.ATTACK;
                ai = new AIAttack(tile);
                break;
            case UnitTeam.DEFENSE:
                Team = UnitTeam.DEFENSE;
                ai = new AIDefense(tile);
                break;
            case UnitTeam.BASE:
                Team = UnitTeam.BASE;
                ai = new AIBase(tile);
                break;
        }
    }

    public string GetAction()
    {
        return ai.GetAction();
    }

    public bool IsInPosition()
    {
        return ai.IsInPosition();
    }

    public override string ToString()
    {
        return "I am a member of " + Team + " located at [" + Tile.X + "," + Tile.Y + "]!";
    }
}

/**
 * Keeps track of the spawn orders from the teams and prioritize them per team in case there isn't enough matter
 */
public class UnitFactory
{
    List<Tile> AttackTeamSpawnRequests;
    List<Tile> DefenseTeamSpawnRequests;
    List<Tile> BaseTeamSpawnRequests;
    Logger logger = Logger.Instance;

    public UnitFactory()
    {
        AttackTeamSpawnRequests = new List<Tile>();
        DefenseTeamSpawnRequests = new List<Tile>();
        BaseTeamSpawnRequests = new List<Tile>();
    }

    public void RequestSpawn(UnitTeam team, Tile tile)
    {
        switch(team)
        {
            case UnitTeam.ATTACK:
                AttackTeamSpawnRequests.Add(tile);
                break;
            case UnitTeam.DEFENSE:
                DefenseTeamSpawnRequests.Add(tile);
                break;
            case UnitTeam.BASE:
                BaseTeamSpawnRequests.Add(tile);
                break;

        }
    }

    public void SpawnUnits()
    {
        SpawnTeamRequests(DefenseTeamSpawnRequests);
        SpawnTeamRequests(AttackTeamSpawnRequests);
        SpawnTeamRequests(BaseTeamSpawnRequests);
    }

    void SpawnTeamRequests(List<Tile> teamRequests)
    {
        foreach (Tile tile in teamRequests)
        {
            logger.LogAction(Action.Spawn(1, tile.X, tile.Y));
        }
    }
}

public class TeamManager
{
    public AttackTeam AttackTeam { get; private set; }
    public DefenseTeam DefenseTeam { get; private set; }
    public BaseTeam BaseTeam { get; private set; }
    public List<Unit> FullTeam { get; }

    public TeamManager()
    {
        AttackTeam = new AttackTeam();
        DefenseTeam = new DefenseTeam();
        BaseTeam = new BaseTeam();
        FullTeam = new List<Unit>();
    }

    public void AssignMembersToTeams(List<Tile> unitTiles)
    {
        //TODO here is complex logic
        foreach (var tile in unitTiles)
        {
            for (var i = 0; i < tile.Units; i++)
            {
                FullTeam.Add(DefenseTeam.AddNewMember(tile));
            }
        }
    }

    public void ManageTeamsActions()
    {
        SendTeamsSpawnRequestsToFactory();
        MoveTeams();
    }

    void SendTeamsSpawnRequestsToFactory()
    {
        AttackTeam.MoveMembers();
        DefenseTeam.MoveMembers();
        BaseTeam.MoveMembers();
    }

    void MoveTeams()
    {
        AttackTeam.SendSpawnRequestsToFactory();
        DefenseTeam.SendSpawnRequestsToFactory();
        BaseTeam.SendSpawnRequestsToFactory();
    }
}

public abstract class Team
{
    public List<Unit> Members { get; protected set; }
    protected UnitTeam TeamType;
    protected GameInstance gameInstance = GameInstance.Instance;
    Logger logger = Logger.Instance;

    public Unit AddNewMember(Tile tile)
    {
        var result = new Unit(TeamType, tile);
        Members.Add(result);
        return result;
    }

    public void MoveMembers()
    {
        foreach (var unit in Members)
        {
            logger.LogAction(unit.GetAction());
        }
    }

    public abstract void SendSpawnRequestsToFactory();
}

public class AttackTeam : Team
{
    public AttackTeam()
    {
        Members = new List<Unit>();
        TeamType = UnitTeam.ATTACK;
    }

    public override void SendSpawnRequestsToFactory()
    {

    }
}

public class DefenseTeam : Team
{
    public DefenseTeam()
    {
        Members = new List<Unit>();
        TeamType = UnitTeam.DEFENSE;
    }

    public override void SendSpawnRequestsToFactory()
    {
        foreach (var unit in Members)
        {
            if (unit.IsInPosition())
            {
                gameInstance.UnitFactory.RequestSpawn(TeamType, unit.Tile);
            }
        }
    }
}

public class BaseTeam : Team
{
    public BaseTeam()
    {
        Members = new List<Unit>();
        TeamType = UnitTeam.BASE;
    }

    public override void SendSpawnRequestsToFactory()
    {
        
    }
}

public class RecyclerFactory
{
    List<Tile> recyclers;
    List<Tile> buildableTiles;
    Logger logger = Logger.Instance;
    GameInstance gameInstance = GameInstance.Instance;

    public RecyclerFactory()
    {
        recyclers = new List<Tile>();
        buildableTiles = new List<Tile>();
    }

    public void AddRecycler(Tile recycler)
    {
        recyclers.Add(recycler);
    }



    public void AddBuildableTile(Tile buildableTile)
    {
        buildableTiles.Add(buildableTile);
    }

    /**
     * We build a recycler if we have enough matter, we did not reach our quota of recyclers, and we can find a suitable place
     */
    public void BuildRecyclersIfNeeded()
    {
        if (recyclers.Count < Constants.IDEAL_RECYCLERS 
            && gameInstance.MyMatter > Constants.MIN_MATTER_TO_BUILD)
        {
            var suitableTile = FindSuitableTile();

            if (suitableTile != null)
            {
                logger.LogAction(Action.Build(suitableTile.X, suitableTile.Y));
            }
        }
    }

    /**
     * A tile is suitable to build if it is not too close from another recycler and it has enough srappable matter
     */
    Tile FindSuitableTile()
    {
        foreach (var tile in buildableTiles)
        {
            if (tile.TotalScrappableAmount > Constants.MIN_SCRAPPABLE_TO_BUILD
                && !IsCloseToRecycler(tile))
            {
                return tile;
            }
        }
        return null;
    }


    /**
     * If we are within range of another of our recyclers, then it is too close
     */
    bool IsCloseToRecycler(Tile tile)
    {
        foreach (var recycler in recyclers)
        {
            if ((tile.X == recycler.X && Math.Abs(tile.Y - recycler.Y) <= 2)
                || (tile.Y == recycler.Y && Math.Abs(tile.X - recycler.X) <= 2)
                || (Math.Abs(tile.X - recycler.X) <= 1 && Math.Abs(tile.Y - recycler.Y) <= 1))
            {
                return true;
            }
        }

        return false;
    }
}

public static class Action
{
    public static string Move(int amount, int fromX, int fromY, int toX, int toY)
    {
        return Constants.MOVE + " " + amount + " " + fromX + " " + fromY + " " + toX + " " + toY;
    }

    public static string Build(int x, int y)
    {
        return Constants.BUILD + " " + x + " " + y;
    }

    public static string Spawn(int amount, int x, int y)
    {
        return Constants.SPAWN + " " + amount + " " + x + " " + y;
    }

    public static string Wait()
    {
        return Constants.WAIT;
    }
}

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

    public void LogDebugMessage(string message)
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
            Console.WriteLine(Action.Wait());
        }
        Output.Clear();
    }

}

public class Tile
{
    public Tile(int x, int y, int scrapAmount, int owner, int units, bool recycler, bool canBuild, bool canSpawn, bool inRangeOfRecycler)
    {
        X = x;
        Y = y;
        ScrapAmount = scrapAmount;
        Owner = owner;
        Units = units;
        Recycler = recycler;
        CanBuild = canBuild;
        CanSpawn = canSpawn;
        InRangeOfRecycler = inRangeOfRecycler;
    }

    public int X { get; }
    public int Y { get; }
    public int ScrapAmount { get; }
    public int Owner { get; }
    public int Units { get; }
    public bool Recycler { get; }
    public bool CanBuild { get; }
    public bool CanSpawn { get; }
    public bool InRangeOfRecycler { get; }
    GameInstance gameInstance = GameInstance.Instance;
    Tile[,] map = GameInstance.Instance.Map;

    public int TotalScrappableAmount
    {
        get
        {
            if (fTotalScrappableAmount == 0)
            {
                if (X < gameInstance.MapWidth - 1)
                {
                    fTotalScrappableAmount += map[X + 1, Y]?.ScrapAmount ?? 0;
                }
                if (X > 0)
                {
                    fTotalScrappableAmount += map[X - 1, Y]?.ScrapAmount ?? 0;
                }
                if (Y < gameInstance.MapHeight - 1)
                {
                    fTotalScrappableAmount += map[X, Y + 1]?.ScrapAmount ?? 0;
                }
                if (Y > 0)
                {
                    fTotalScrappableAmount += map[X, Y - 1]?.ScrapAmount ?? 0;
                }

                fTotalScrappableAmount += ScrapAmount;
            }
            return fTotalScrappableAmount;
        }
    }
    int fTotalScrappableAmount = 0;

    public override string ToString()
    {
        return "x: " + X + ", y:" + Y;
    }
}