using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

// CheckersService
[ServiceContract]
public interface IRestCheckersService
{
    /******  REST calls ******/
    // Players REST calls
    [OperationContract]
    [WebInvoke(Method = "PUT", UriTemplate = "/players/add", ResponseFormat = WebMessageFormat.Json)]
    bool AddPlayer(Player player);

    [OperationContract]
    [WebInvoke(Method = "DELETE", UriTemplate = "/players/delete?playerId={playerId}", ResponseFormat = WebMessageFormat.Json)]
    bool RemovePlayer(string playerId);

    [OperationContract]
    [WebInvoke(Method = "POST", UriTemplate = "/players/update", ResponseFormat = WebMessageFormat.Json)]
    bool UpdatePlayer(Player player);

    [OperationContract]
    [WebGet(UriTemplate = "/players/get?playerId={playerId}")]
    Player GetPlayerById(string playerId);

    [OperationContract]
    [WebGet(UriTemplate = "/players/getAll", ResponseFormat = WebMessageFormat.Json)]
    List<Player> GetPlayers();

    [OperationContract]
    [WebGet(UriTemplate = "/players/byGame?gameId={gameId}", ResponseFormat = WebMessageFormat.Json)]
    List<Player> GetPlayersByGame(string gameId);

    [OperationContract]
    [WebInvoke(Method = "DELETE", UriTemplate = "/games/delete?gameId={gameId}", ResponseFormat = WebMessageFormat.Json)]
    bool RemoveGame(string gameId);

    [OperationContract]
    [WebInvoke(Method = "POST", UriTemplate = "/games/update", ResponseFormat = WebMessageFormat.Json)]
    bool UpdateGame(Game game);

    // Games REST Calls
    [OperationContract]
    [WebInvoke(Method = "PUT", UriTemplate = "/games/add", ResponseFormat = WebMessageFormat.Json)]
    bool AddGame(Game game);

    [OperationContract]
    [WebGet(UriTemplate = "/games/getAll", ResponseFormat = WebMessageFormat.Json)]
    List<Game> GetGames();

    [OperationContract]
    [WebGet(UriTemplate = "/games/get?gameId={gameId}")]
    Game GetGameById(string gameId);

    [OperationContract]
    [WebGet(UriTemplate = "/games/byPlayer?playerId={playerId}", ResponseFormat = WebMessageFormat.Json)]
    List<Game> GetGamesByPlayer(string playerId);


    [OperationContract]
    [WebGet(UriTemplate = "/games/count?playerId={playerId}", ResponseFormat = WebMessageFormat.Json)]
    int GetTotalGamesCountForPlayer(string playerId);

    [OperationContract]
    [WebInvoke(Method = "PUT", UriTemplate = "/families/add", ResponseFormat = WebMessageFormat.Json)]
    bool AddFamily(Family family);

    [OperationContract]
    [WebInvoke(Method = "DELETE", UriTemplate = "/families/delete?familyId={familyId}", ResponseFormat = WebMessageFormat.Json)]
    bool DeleteFamily(string familyId);

    [OperationContract]
    [WebGet(UriTemplate = "/families/get?familyId={familyId}", ResponseFormat = WebMessageFormat.Json)]
    Family GetFamily(string familyId);

    [OperationContract]
    [WebGet(UriTemplate = "/families/byPlayer?playerId={familyId}", ResponseFormat = WebMessageFormat.Json)]
    List<Player> GetPlayersByFamily(string familyId);

    [OperationContract]
    [WebGet(UriTemplate = "/moves/byGame?gameId={gameId}")]
    List<Move> GetMovesByGame(string gameId);

}

[ServiceContract]
public interface ISoapCheckersService
{
    [OperationContract]
    List<Move> RecoverGameMovesByPlayer(Game game, Player player);
}



[ServiceContract(CallbackContract = typeof(IDuplexCheckersServiceCallback), SessionMode = SessionMode.Required)]
public interface IDuplexCheckersService
{
    [OperationContract(IsOneWay = true)]
    void MakeMove(Move move);

    [OperationContract(IsOneWay = true)]
    void Login(string name, string password);

    [OperationContract(IsOneWay = true)]
    void StartGame(string gameId);

    [OperationContract]
    void SaveInitialPositions(List<Move> initialPositions, Status gameStatus);

}

public interface IDuplexCheckersServiceCallback
{
    // Duplex client calls
    [OperationContract(IsOneWay = true)]
    void MakeMoveCallback(List<Move> availableMoves, Status status);

    [OperationContract(IsOneWay = true)]
    void LoginCallback(Player player, Status status);

    [OperationContract(IsOneWay = true)]
    void StartGameCallback(Game game, Status status);

    [OperationContract(IsOneWay = true)]
    void PlayerTurnCallback();
}

// Use a data contract as illustrated in the sample below to add composite types to service operations.
[DataContract]
public enum Status {
    [EnumMember]
    MOVE_ACCEPTED,
    [EnumMember]
    GAME_LOSE,
    [EnumMember]
    GAME_WIN,
    [EnumMember]
    GAME_STARTED,
    [EnumMember]
    NO_SUCH_GAME,
    [EnumMember]
    WAITING_FOR_OTHER_PLAYER_TO_ARRIVE,
    [EnumMember]
    NOT_ENOUGH_PLAYERS_TO_START_GAME,
    [EnumMember]
    GAME_ALREADY_STARTED_BY_OTHER_PLAYERS,
    [EnumMember]
    NOT_LOGGED_IN,
    [EnumMember]
    NO_SUCH_USER,
    [EnumMember]
    WRONG_INPUT,
    [EnumMember]
    LOGIN_SUCCEDED
}

[DataContract]
public class Player
{

    [DataMember]
    public int Id { get; set; }
    [DataMember]
    public string Name { get; set; }
    [DataMember]
    public string Password { set; get; }
    [DataMember]
    public Family Family { get; set; }
}

[DataContract]
public class Game
{
    [DataMember]
    public int Id { get; set; }
    [DataMember]
    public DateTime CreatedDateTime { get; set; }
    [DataMember]
    public Player Player1 { get; set; }
    [DataMember]
    public Player Player2 { get; set; }
    
}

[DataContract]
public class Family
{
    [DataMember]
    public int Id { get; set; }
    [DataMember]
    public string Name { get; set; }
}

[DataContract]
public class Move
{
    [DataMember]
    public int Id { get; set; }
    [DataMember]
    public int GameId { get; set; }
    [DataMember]
    public int PlayerId { get; set; }
    [DataMember]
    public DateTime DateTime { get; set; }
    [DataMember]
    public Coordinate From { get; set; }
    [DataMember]
    public Coordinate To { get; set; }
}

[DataContract]
public class Coordinate
{
    [DataMember]
    public int X { get; set; }
    [DataMember]
    public int Y { get; set; }

    public Coordinate() { }

    public Coordinate(int X, int Y)
    {
        this.X = X; this.Y = Y;
    }

    public override bool Equals(object obj)
    {
        if (obj is Coordinate)
        {
            Coordinate other = (Coordinate)obj;
            return (X == other.X && Y == other.Y);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return X.GetHashCode() & Y.GetHashCode();
    }

    public override string ToString()
    {
        return string.Format("({0,3}, {1,3})", X, Y);
    }

}