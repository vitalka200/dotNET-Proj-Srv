using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

// CheckersService
[ServiceContract(CallbackContract = typeof(ICheckersServiceCallback), SessionMode = SessionMode.Required)]
public interface ICheckersService
{
    /******  REST calls ******/
    // Players REST calls
    [OperationContract]
    [WebInvoke(Method = "PUT", UriTemplate = "/players", ResponseFormat = WebMessageFormat.Json)]
    void AddPlayers(List<Player> players);

    [OperationContract]
    [WebInvoke(Method = "PUT", UriTemplate = "/players", ResponseFormat = WebMessageFormat.Json)]
    void AddPlayer(Player player);

    [OperationContract]
    [WebInvoke(Method = "DELETE", UriTemplate = "/players?id={playerId}", ResponseFormat = WebMessageFormat.Json)]
    void RemovePlayer(int playerId);

    [OperationContract]
    [WebInvoke(Method = "POST", UriTemplate = "/players/update", ResponseFormat = WebMessageFormat.Json)]
    void UpdatePlayer(Player player);


    [OperationContract]
    [WebGet(UriTemplate = "/players", ResponseFormat = WebMessageFormat.Json)]
    List<Player> GetPlayers();

    [OperationContract]
    [WebGet(UriTemplate = "/players?gameId={gameId}", ResponseFormat = WebMessageFormat.Json)]
    List<Player> GetPlayerByGame(int gameId);

    [OperationContract]
    [WebInvoke(Method = "DELETE", UriTemplate = "/games?id={gameId}", ResponseFormat = WebMessageFormat.Json)]
    void RemoveGame(int gameId);

    [OperationContract]
    [WebInvoke(Method = "POST", UriTemplate = "/games/update", ResponseFormat = WebMessageFormat.Json)]
    void UpdateGame(Game game);

    // Games REST Calls
    [OperationContract]
    [WebInvoke(Method = "PUT", UriTemplate = "/games/add", ResponseFormat = WebMessageFormat.Json)]
    void AddGame(Game game);

    [OperationContract]
    [WebGet(UriTemplate = "/games", ResponseFormat = WebMessageFormat.Json)]
    List<Game> GetGames();

    [OperationContract]
    [WebGet(UriTemplate = "/games?playerId={playerId}", ResponseFormat = WebMessageFormat.Json)]
    List<Game> GetGamesByPlayer(int playerId);


    [OperationContract]
    [WebGet(UriTemplate = "/games/count?playerId={playerId}", ResponseFormat = WebMessageFormat.Json)]
    int GetTotalGamesCountForPlayer(int playerId);

}

public interface ICheckersServiceCallback
{
    // Duplex client calls

}

// Use a data contract as illustrated in the sample below to add composite types to service operations.
[DataContract]
public class Player
{
    int id { get; set; }
    string name { get; set; }
    string familyId { get; set; }

}

[DataContract]
public class Game
{
    string id { get; set; }
    DateTime createdDateTime { get; set; }
    
}

[DataContract]
public class Move
{
    int id { get; set; }
    int gameId { get; set; }
    int playerId { get; set; }
    DateTime dateTime { get; set; }
}