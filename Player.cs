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
    public const int SCRAP_TO_BUILLD_OR_SPAWN = 10;
    public const int MIN_MATTER_TO_BUILD = 20;
    public const int MIN_MATTER_TO_SPAWN = 20;
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
    MIDDLE = 0,
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
    public int MyMatter { get; set; }
    public int BaseTeamObjectiveIndex { get; set; }
    public SortedList<Tile, Tile> SortedConvertibleTiles { get; private set; }
    public List<Tile> ConvertibleTilesOnDefenseLine { get; private set; }
    public List<Tile> SpawnableTiles { get; private set; }
    public CampPosition MyCampPosition { get; private set; }
    public CampPosition EnemyCampPosition { get; private set; }
    public UnitManager UnitManager { get; private set; }
    public TeamsManager TeamsManager { get; private set; }
    public RecyclerFactory RecyclerFactory { get; private set; }


    public void Start()
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        MapWidth = int.Parse(inputs[0]);
        MapHeight = int.Parse(inputs[1]);
        MyCampPosition = CampPosition.MIDDLE;
        EnemyCampPosition = CampPosition.MIDDLE;
        TeamsManager = new TeamsManager();
        RecyclerFactory = new RecyclerFactory();
        UnitManager = new UnitManager();
        ConvertibleTilesOnDefenseLine = new List<Tile>();
        SpawnableTiles = new List<Tile>();

        // game loop
        while (true)
        {
            inputs = Console.ReadLine().Split(' ');
            MyMatter = int.Parse(inputs[0]);
            int oppMatter = int.Parse(inputs[1]);
            Map = new Tile[MapWidth, MapHeight];

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
                                UnitManager.CreateUnits(tile, tile.Units);
                            }

                            if (tile.Recycler)
                            {
                                RecyclerFactory.AddRecycler(tile);
                            }
                            else
                            {
                                SpawnableTiles.Add(tile);
                            }

                            if (tile.CanBuild)
                            {
                                RecyclerFactory.AddBuildableTile(tile);
                            }
                        }
                        else if (tile.Owner == Constants.ENEMY)
                        {
                            TeamsManager.AttackTeam.AddEnemyTile(tile);
                        }

                        if (!tile.Recycler && tile.Owner != Constants.ME)
                        {
                            if (MyCampPosition != CampPosition.MIDDLE)
                            {
                                SortedConvertibleTiles.Add(tile, tile);
                            }
                            
                            if (j == TeamsManager.DefenseTeam.DefenseLine)
                            {
                                ConvertibleTilesOnDefenseLine.Add(tile);
                            }
                        }
                    }
                }
            }

            if (MyCampPosition == CampPosition.MIDDLE)
            {
                switch (UnitManager.Units[0].Tile.X < MapWidth / 2)
                {
                    case true:
                        MyCampPosition = CampPosition.LEFT;
                        EnemyCampPosition = CampPosition.RIGHT;
                        SortedConvertibleTiles = new SortedList<Tile, Tile>(new SortTileFromLeftToRight());
                        break;
                    case false:
                        MyCampPosition = CampPosition.RIGHT;
                        EnemyCampPosition = CampPosition.LEFT;
                        SortedConvertibleTiles = new SortedList<Tile, Tile>(new SortTileFromRightToLeft());
                        break;
                }

                BaseTeamObjectiveIndex = 0;
            }

            //hack to test against myself
            //if (MyCampPosition == CampPosition.LEFT)
            //{
            //    Console.WriteLine("WAIT");
            //}
            //else
            {
                RecyclerFactory.BuildRecyclersIfNeeded();

                TeamsManager.AssignMembersToTeams(UnitManager.Units);
                TeamsManager.ManageTeamsActions();

                //Helper.LogTeamCompositions(logger, UnitManager);

                if (ConvertibleTilesOnDefenseLine.Count == 0)
                {
                    if (TeamsManager.DefenseTeam.Members.FirstOrDefault(u => u.Camp == EnemyCampPosition) is Unit unit)
                    {
                        UnitManager.RequestSpawn(UnitTeam.DEFENSE, unit.Tile, MyMatter / Constants.SCRAP_TO_BUILLD_OR_SPAWN);
                        Logger.LogDebugMessage("Requesting " + (MyMatter / Constants.SCRAP_TO_BUILLD_OR_SPAWN) + " units at " + unit.Tile);
                    }
                }

                UnitManager.SpawnUnits();

                logger.PublishOutput();
            }

            ResetBeforeNewTurn();
        }
    }

    void ResetBeforeNewTurn()
    {
        TeamsManager.ResetForNewTurn();
        RecyclerFactory.ResetForNewTurn();
        UnitManager.ResetForNewTurn();
        SortedConvertibleTiles.Clear();
        ConvertibleTilesOnDefenseLine.Clear();
        SpawnableTiles.Clear();
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
                Logger.LogDebugMessage("Tile [" + x + "," + y + "]: Total: " + map[x, y].TotalScrappableAmount);
                i++;
            }
        }
    }

    public static void LogTeamCompositions(Logger logger, UnitManager unitManager)
    {
        foreach (var unit in unitManager.Units)
        {
            Logger.LogDebugMessage(unit.ToString());
        }
    }

    public static CampPosition GetCampForTile(GameInstance gameInstance, int x)
    {
        CampPosition result;
        if (x > (gameInstance.MapWidth / 2))
        {
            result = CampPosition.RIGHT;

        }
        else if (x < (gameInstance.MapWidth / 2))
        {
            result = CampPosition.LEFT;
        }
        else
        {
            result = CampPosition.MIDDLE;
        }
        return result;
    }

    /**
     * A tile is valid if it is neither grass nor a recycler
     */
    public static Tile FindClosestValidTile(int x, int y, int furthestYToTest)
    {
        Logger.LogDebugMessage("Closest valid tile from : " + x + " " + y);

        var gameInstance = GameInstance.Instance;

        var result = gameInstance.Map[x, y];

        if (result != null && !result.Recycler)
        {
            Logger.LogDebugMessage("is itself");
            return result;
        }

        var direction = furthestYToTest - y < 0 ? -1 : 1;
        var n = y;

        do
        {
            n += direction;
            result = gameInstance.Map[x, n];
            if (result != null && !result.Recycler)
            {
                Logger.LogDebugMessage("is : " + x + " " + n);
                return result;
            }

        }
        while (n != furthestYToTest);

        //if we couldn't find any, we return null
        Logger.LogDebugMessage("is not found");
        return result;
    }
}

public interface IAI
{
    string GetAction();
    void CalculateTarget();
}

public abstract class AI : IAI
{
    public string GetAction()
    {
        CalculateTarget();
        return Action.Move(1, Location.X, Location.Y, Target.X, Target.Y);
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
    }

    public override void CalculateTarget()
    {
        foreach (var tile in GameInstance.Instance.TeamsManager.AttackTeam.EnemyTiles.Keys)
        { 
            if (!tile.Recycler)
            {
                Target = tile;
                return;
            }
        }
    }
}

public class AIDefense : AI
{
    public AIDefense(Tile tile)
    {
        Location = tile;
    }

    public override void CalculateTarget()
    {
        var defenseTeam = GameInstance.TeamsManager.DefenseTeam;
        if (defenseTeam.HasTeamArrived)
        {
            if (GameInstance.ConvertibleTilesOnDefenseLine.Count > 0)
            {
                if (Location.Y == defenseTeam.BottomUnitTile.Y)
                {
                    Target = GameInstance.ConvertibleTilesOnDefenseLine.LastOrDefault() ?? Location;
                }
                else if (Location.Y == defenseTeam.TopUnitTile.Y)
                {
                    Target = GameInstance.ConvertibleTilesOnDefenseLine.FirstOrDefault() ?? Location;
                }
                else
                {
                    Target = GameInstance.TeamsManager.AttackTeam.EnemyTiles.Keys.FirstOrDefault() ?? Location;
                }
            }
            else
            {
                Target = GameInstance.TeamsManager.AttackTeam.EnemyTiles.Keys.FirstOrDefault() ?? Location;
            }
        }
        else
        {
            Target = Helper.FindClosestValidTile(defenseTeam.DefenseLine, Location.Y, Location.Y > GameInstance.MapHeight / 2 ? GameInstance.MapHeight : 0);
        }
    }
}

public class AIBase : AI
{
    public AIBase(Tile tile)
    {
        Location = tile;
    }

    public override void CalculateTarget()
    {
        var calculatedTarget = GameInstance.SortedConvertibleTiles.Keys.FirstOrDefault();
        if (calculatedTarget == null)
        {
            Target = Location;
        }
        else
        {
            Target = calculatedTarget;
        }
    }
}

public class Unit
{
    public Tile Tile { get; }
    public UnitTeam Team { get; private set; }
    public CampPosition Camp => Tile.Camp;
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

    public override string ToString()
    {
        return "I am a member of " + Team + " located at [" + Tile.X + "," + Tile.Y + "], in " + Camp + " Camp!";
    }
}

/**
 * Keeps track of the spawn orders from the teams and prioritize them per team in case there isn't enough matter
 */
public class UnitManager
{
    public List<Unit> Units { get; private set; }
    Dictionary<Tile, int> AttackTeamSpawnRequests;
    Dictionary<Tile, int> DefenseTeamSpawnRequests;
    Dictionary<Tile, int> BaseTeamSpawnRequests;
    Logger logger = Logger.Instance;

    public UnitManager()
    {
        AttackTeamSpawnRequests = new Dictionary<Tile, int>();
        DefenseTeamSpawnRequests = new Dictionary<Tile, int>();
        BaseTeamSpawnRequests = new Dictionary<Tile, int>();
        Units = new List<Unit>();
    }

    public void ResetForNewTurn()
    {
        AttackTeamSpawnRequests.Clear();
        DefenseTeamSpawnRequests.Clear();
        BaseTeamSpawnRequests.Clear();
        Units.Clear();
    }

    public void CreateUnits(Tile tile, int unitCount)
    {
        for (var i = 0; i < unitCount; i++)
        {
            Units.Add(new Unit(tile));
        }
    }

    public void RequestSpawn(UnitTeam team, Tile tile, int unitCount)
    {
        switch(team)
        {
            case UnitTeam.ATTACK:
                AttackTeamSpawnRequests.TryAdd(tile, unitCount);
                break;
            case UnitTeam.DEFENSE:
                DefenseTeamSpawnRequests.TryAdd(tile, unitCount);
                break;
            case UnitTeam.BASE:
                BaseTeamSpawnRequests.TryAdd(tile, unitCount);
                break;

        }
    }

    public void SpawnUnits()
    {
        SpawnTeamRequests(DefenseTeamSpawnRequests);
        SpawnTeamRequests(AttackTeamSpawnRequests);
        SpawnTeamRequests(BaseTeamSpawnRequests);
    }

    void SpawnTeamRequests(Dictionary<Tile, int> teamRequests)
    {
        foreach (var unitsToSpawn in teamRequests)
        {
            logger.LogAction(Action.Spawn(unitsToSpawn.Value, unitsToSpawn.Key.X, unitsToSpawn.Key.Y));
            GameInstance.Instance.MyMatter -= unitsToSpawn.Value * Constants.SCRAP_TO_BUILLD_OR_SPAWN;
        }
    }
}

public class TeamsManager
{
    public AttackTeam AttackTeam { get; private set; }
    public DefenseTeam DefenseTeam { get; private set; }
    public BaseTeam BaseTeam { get; private set; }

    public TeamsManager()
    {
        AttackTeam = new AttackTeam();
        DefenseTeam = new DefenseTeam();
        BaseTeam = new BaseTeam();
    }

    public void ResetForNewTurn()
    {
        AttackTeam.ResetTeamForNewTurn();
        DefenseTeam.ResetTeamForNewTurn();
        BaseTeam.ResetTeamForNewTurn();
    }

    public void AssignMembersToTeams(List<Unit> units)
    {
        if (units.Count == 0)
        {
            var tileToSpawnTo = GameInstance.Instance.SpawnableTiles.FirstOrDefault();
            var qty = GameInstance.Instance.MyMatter / Constants.SCRAP_TO_BUILLD_OR_SPAWN;
            GameInstance.Instance.UnitManager.RequestSpawn(UnitTeam.DEFENSE, tileToSpawnTo, qty);
            GameInstance.Instance.UnitManager.CreateUnits(tileToSpawnTo, qty);
        }

        //TODO here is complex logic
        var baseUnits = GameInstance.Instance.MyCampPosition == CampPosition.LEFT ? units.OrderBy(t => t.Tile.X).Take(Constants.BASE_TEAM_UNITS) : units.OrderByDescending(t => t.Tile.X).Take(Constants.BASE_TEAM_UNITS);
        var attackUnit = GameInstance.Instance.MyCampPosition == CampPosition.RIGHT ? units.OrderBy(t => t.Tile.X).First() : units.OrderByDescending(t => t.Tile.X).First();

        //AttackTeam.AddNewMember(attackUnit);
        //units.Remove(attackUnit);

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
        MoveTeams();
    }

    void MoveTeams()
    {
        AttackTeam.MoveMembers();
        DefenseTeam.MoveMembers();
        BaseTeam.MoveMembers();
    }
}

public abstract class Team
{
    public List<Unit> Members { get; protected set; }
    protected UnitTeam TeamType;
    protected GameInstance gameInstance = GameInstance.Instance;
    Logger logger = Logger.Instance;

    public virtual void ResetTeamForNewTurn()
    {
        Members.Clear();
    }

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
}

public class AttackTeam : Team
{
    public SortedList<Tile, Tile> EnemyTiles { get; private set; }

    public AttackTeam()
    {
        Members = new List<Unit>();
        TeamType = UnitTeam.ATTACK;

        if (gameInstance.MyCampPosition == CampPosition.LEFT)
        {
            EnemyTiles = new SortedList<Tile, Tile>(new SortTileFromRightToLeft());
        }
        else
        {
            EnemyTiles = new SortedList<Tile, Tile>(new SortTileFromLeftToRight());
        }
    }

    public override void ResetTeamForNewTurn()
    {
        base.ResetTeamForNewTurn();

        EnemyTiles.Clear();
    }

    public void AddEnemyTile(Tile enemyTile)
    {
        EnemyTiles.Add(enemyTile, null);
    }
}

public class DefenseTeam : Team
{
    public Tile TopUnitTile { get; private set; }
    public Tile BottomUnitTile { get; private set; }
    public int DefenseLine { get; private set; }
    public bool HasTeamArrived => hasTeamArrived;
    bool hasTeamArrived = false;
    int unitsOnDefenseLine = 0;

    public DefenseTeam()
    {
        Members = new List<Unit>();
        TeamType = UnitTeam.DEFENSE;
        DefenseLine = gameInstance.MapWidth / 2;
    }

    public override void ResetTeamForNewTurn()
    {
        base.ResetTeamForNewTurn();

        TopUnitTile = null;
        BottomUnitTile = null;
        unitsOnDefenseLine = 0;
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

        if (tile.X == DefenseLine)
        {
            unitsOnDefenseLine++;

            if (unitsOnDefenseLine >= 2)
            {
                hasTeamArrived = true;
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
}

public class RecyclerFactory
{
    List<Tile> recyclers;
    SortedList<Tile, Tile> buildableTiles;
    Logger logger = Logger.Instance;
    GameInstance gameInstance = GameInstance.Instance;

    public RecyclerFactory()
    {
        recyclers = new List<Tile>();
        if (gameInstance.MyCampPosition == CampPosition.LEFT)
        {
            buildableTiles = new SortedList<Tile, Tile>(new SortTileFromLeftToRight());
        }
        else
        {
            buildableTiles = new SortedList<Tile, Tile>(new SortTileFromRightToLeft());
        }
    }

    public void ResetForNewTurn()
    {
        recyclers.Clear();
        buildableTiles.Clear();
    }

    public void AddRecycler(Tile recycler)
    {
        recyclers.Add(recycler);
    }

    public void AddBuildableTile(Tile buildableTile)
    {
        buildableTiles.Add(buildableTile, null);
    }

    /**
     * We build a recycler if we have enough matter, we did not reach our quota of recyclers, and we can find a suitable place
     */
    public void BuildRecyclersIfNeeded()
    {
        //if (recyclers.Count < Constants.IDEAL_RECYCLERS 
        //    && gameInstance.MyMatter > Constants.MIN_MATTER_TO_BUILD)
        {
            var suitableTiles = FindSuitableTiles();

            foreach (var suitableTile in suitableTiles)
            {
                logger.LogAction(Action.Build(suitableTile.X, suitableTile.Y));
                gameInstance.MyMatter -= Constants.SCRAP_TO_BUILLD_OR_SPAWN;
            }
        }
    }

    /**
     * A tile is suitable to build if it is not too close from another recycler and it has enough scrappable matter
     */
    IEnumerable<Tile> FindSuitableTiles()
    {
        foreach (var tile in buildableTiles.Keys)
        {
            //if (tile.TotalScrappableAmount > Constants.MIN_SCRAPPABLE_TO_BUILD
            //    && !IsCloseToRecycler(tile))
            if ((tile.X >= gameInstance.TeamsManager.DefenseTeam.DefenseLine && gameInstance.MyCampPosition == CampPosition.LEFT)
                || (tile.X <= gameInstance.TeamsManager.DefenseTeam.DefenseLine && gameInstance.MyCampPosition == CampPosition.RIGHT))
            {
                yield return tile;
            }
        }
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

    public CampPosition Camp 
    {
        get
        {
            if (!camp.HasValue)
            {
                camp = Helper.GetCampForTile(gameInstance, X);
            }
            return camp.Value;
        }
    }
    CampPosition? camp;

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

class SortTileFromLeftToRight : IComparer<Tile>
{
    int IComparer<Tile>.Compare(Tile a, Tile b)
    {
        var t1 = (Tile)a;
        var t2 = (Tile)b;
        if (t1.X > t2.X)
        {
            return 1;
        }
        if (t1.X < t2.X)
        {
            return -1;
        }
        else
        {

            if (t1.Y > t2.Y)
            {
                return 1;
            }
            if (t1.Y < t2.Y)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }
    }
}

class SortTileFromRightToLeft : IComparer<Tile>
{
    int IComparer<Tile>.Compare(Tile a, Tile b)
    {
        var t1 = (Tile)a;
        var t2 = (Tile)b;
        if (t1.X < t2.X)
        {
            return 1;
        }
        if (t1.X > t2.X)
        {
            return -1;
        }
        else
        {
            if (t1.Y > t2.Y)
            {
                return 1;
            }
            if (t1.Y < t2.Y)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }
    }
}