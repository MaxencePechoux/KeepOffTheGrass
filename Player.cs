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
    public const int BASE_TEAM_UNITS = 1;
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

public enum Direction
{
    UP = -1,
    DOWN = 1
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
    public int DefenseLine { get; private set; }
    public int BaseTeamObjectiveIndex { get; set; }
    public List<Tile> BaseTeamObjectives { get; private set; }
    public CampPosition Direction { get; private set; }
    public UnitManager UnitManager { get; private set; }
    public TeamManager TeamManager { get; private set; }
    RecyclerFactory recyclerFactory;


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
            TeamManager = new TeamManager();
            recyclerFactory = new RecyclerFactory();
            UnitManager = new UnitManager();

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
                                UnitManager.CreateUnits(tile);
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
                Direction = UnitManager.Units[0].Tile.X < MapWidth / 2 ? CampPosition.LEFT : CampPosition.RIGHT;
                BaseTeamObjectives = Helper.InitBaseTeamObjectives();
                BaseTeamObjectiveIndex = 0;
            }

            recyclerFactory.BuildRecyclersIfNeeded();

            TeamManager.AssignMembersToTeams(UnitManager.Units);
            TeamManager.ManageTeamsActions();

            UnitManager.SpawnUnits();

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

    public static void LogTeamCompositions(Logger logger, UnitManager unitManager)
    {
        foreach (var unit in unitManager.Units)
        {
            logger.LogDebugMessage(unit.ToString());
        }
    }

    public static List<Tile> InitBaseTeamObjectives()
    {
        var result = new List<Tile>();

        var gameInstance = GameInstance.Instance;
        var map = gameInstance.Map;

        int x = gameInstance.Direction == CampPosition.LEFT ? 0 : gameInstance.MapWidth - 1;
        for (var i = 0; i < gameInstance.MapWidth / 2; i++)
        {
            int y = i % 2 == 0 ? 0 : gameInstance.MapHeight - 1;
            result.Add(FindClosestNonGrassNorRecyclerTile(map[gameInstance.MapWidth / 2, gameInstance.MapHeight / 2], Math.Abs(x - i), y));
        }

        return result;
    }

    public static Tile FindClosestNonGrassNorRecyclerTile(Tile origin, int x, int y)
    {
        Logger.Instance.LogDebugMessage("Closest tile from : " + x + " " + y);

        var gameInstance = GameInstance.Instance;

        var result = gameInstance.Map[x, y];
        if (result != null && !result.Recycler)
        {
            return result;
        }

        //we might need to recalculate the direction here
        var direction = 1;
        var goal = y > gameInstance.MapHeight / 2 ? gameInstance.MapHeight - 1 : 0;
        var n = y;
        var i = x;

        while (i != origin.X)
        {
            while (n != goal)
            {
                n += direction;
                result = gameInstance.Map[i, n];

                if (result != null && !result.Recycler)
                {
                    return result;
                }
            }

            direction *= -1;
            goal = goal == 0 ? gameInstance.MapHeight - 1 : 0;
            n = y;

            while (n != goal)
            {
                n += direction;
                result = gameInstance.Map[i, n];

                if (result != null && !result.Recycler)
                {
                    return result;
                }
            }

            //If we cannot find a suitable tile in the middle, we try one column closer to the unit
            i -= (int) gameInstance.Direction;
        }

        //If we can't find any, we ll just return the origin
        return origin;
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
        CalculateTarget();
        return Action.Move(1, Location.X, Location.Y, Target.X, Target.Y);
    }

    public abstract bool IsInPosition();
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
    }

    public override void CalculateTarget()
    { }

    public override bool IsInPosition()
    {
        return false;
    }
}

public class AIDefense : AI
{
    public AIDefense(Tile tile)
    {
        Location = tile;
    }
    
    public override bool IsInPosition()
    {
        return Location.X == GameInstance.DefenseLine;
    }

    int CalculateY()
    {
        var result = Location.Y;
        
        if (IsInPosition())
        {
            var defenseTeam = GameInstance.TeamManager.DefenseTeam;

            //for now, we only move the top units up, and the bottom units down
            if (Location.Y == defenseTeam.BottomUnitTile.Y)
            {
                result++;
            }
            else if (Location.Y == defenseTeam.TopUnitTile.Y)
            {
                result--;
            }
        }

        return Math.Min(Math.Max(result, 0), GameInstance.MapHeight - 1);
    }

    public override void CalculateTarget()
    {
        int x = GameInstance.DefenseLine;
        int y = CalculateY();
        Target = Helper.FindClosestNonGrassNorRecyclerTile(Location, x, y);
    }
}

public class AIBase : AI
{
    public AIBase(Tile tile)
    {
        Location = tile;
        Target = GameInstance.BaseTeamObjectives[GameInstance.BaseTeamObjectiveIndex];
    }

    public override void CalculateTarget()
    {
        if (Target.Equals(Location))
        {
            if (GameInstance.BaseTeamObjectiveIndex < GameInstance.BaseTeamObjectives.Count - 1)
            {
                GameInstance.BaseTeamObjectiveIndex++;
                Target = GameInstance.BaseTeamObjectives[GameInstance.BaseTeamObjectiveIndex];
            }
        }
    }

    public override bool IsInPosition()
    {
        return false;
    }
}

public class Unit
{
    public Tile Tile { get; }
    public UnitTeam Team { get; private set; }
    IAI ai;

    public Unit(Tile tile)
    {
        Tile = tile;
    }

    public void AssignTeam(UnitTeam team)
    {
        switch (team)
        {
            case UnitTeam.ATTACK:
                Team = UnitTeam.ATTACK;
                ai = new AIAttack(Tile);
                break;
            case UnitTeam.DEFENSE:
                Team = UnitTeam.DEFENSE;
                ai = new AIDefense(Tile);
                break;
            case UnitTeam.BASE:
                Team = UnitTeam.BASE;
                ai = new AIBase(Tile);
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
public class UnitManager
{
    public List<Unit> Units { get; }
    List<Tile> AttackTeamSpawnRequests;
    List<Tile> DefenseTeamSpawnRequests;
    List<Tile> BaseTeamSpawnRequests;
    Logger logger = Logger.Instance;

    public UnitManager()
    {
        AttackTeamSpawnRequests = new List<Tile>();
        DefenseTeamSpawnRequests = new List<Tile>();
        BaseTeamSpawnRequests = new List<Tile>();
        Units = new List<Unit>();
    }

    public void CreateUnits(Tile tile)
    {
        for (var i = 0; i < tile.Units; i++)
        {
            Units.Add(new Unit(tile));
        }
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

    public TeamManager()
    {
        AttackTeam = new AttackTeam();
        DefenseTeam = new DefenseTeam();
        BaseTeam = new BaseTeam();
    }

    public void AssignMembersToTeams(List<Unit> units)
    {
        //TODO here is complex logic
        var baseUnits = units.OrderBy(t => t.Tile.X).Take(Constants.BASE_TEAM_UNITS);

        foreach (var baseUnit in baseUnits)
        { 
            BaseTeam.AddNewMember(baseUnit);
            units.Remove(baseUnit);
        }
        
        foreach (var unit in units)
        {
            DefenseTeam.AddNewMember(unit);
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

    public virtual void AddNewMember(Unit unit)
    {
        Members.Add(unit);
        unit.AssignTeam(TeamType);
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
    public Tile TopUnitTile {get; private set; }
    public Tile BottomUnitTile {get; private set; }

    public DefenseTeam()
    {
        Members = new List<Unit>();
        TeamType = UnitTeam.DEFENSE;
    }

    public override void AddNewMember(Unit unit)
    {
        base.AddNewMember(unit);

        var tile = unit.Tile;
        if (TopUnitTile == null || TopUnitTile.Y > tile.Y)
        {
            TopUnitTile = tile;
        }
        if (BottomUnitTile == null || BottomUnitTile.Y < tile.Y)
        {
            BottomUnitTile = tile;
        }
    }

    public override void SendSpawnRequestsToFactory()
    {
        foreach (var unit in Members)
        {
            if (unit.IsInPosition())
            {
                gameInstance.UnitManager.RequestSpawn(TeamType, unit.Tile);
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

    public override string ToString()
    {
        return "x: " + X + ", y:" + Y;
    }
}