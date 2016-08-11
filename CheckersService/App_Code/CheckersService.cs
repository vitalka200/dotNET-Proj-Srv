﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
public class CheckersService : IRestCheckersService, IDuplexCheckersService, ISoapCheckersService // Interfaces
{
    private const int MAX_COLLS = 4;
    private const int MAX_ROWS = 8;
    private Coordinate INITIAL_POINT = new Coordinate { X = -1, Y = -1};

    private CheckersDBDataContext db = new CheckersDBDataContext();
    private Dictionary<Player, IDuplexCheckersServiceCallback> lookupPlayer2Callback = new Dictionary<Player, IDuplexCheckersServiceCallback>();
    private Dictionary<IDuplexCheckersServiceCallback, Player> lookupCallback2Player = new Dictionary<IDuplexCheckersServiceCallback, Player>();

    private Object DB_WRITE_LOCK = new Object();

    public bool AddGame(Game game)
    {
        if (game != null && game.Player1 != null && game.Player2 != null)
        {

            TblGame gameToSave = new TblGame { CreatedDate = game.CreatedDateTime };
            db.TblGames.Attach(gameToSave);
            db.SubmitChanges();

            TblPlayerGame[] gameToPlayer = {
                new TblPlayerGame { idGame = gameToSave.Id, idPlayer = game.Player1.Id },
                new TblPlayerGame { idGame = gameToSave.Id, idPlayer = game.Player2.Id }
            };

            db.TblPlayerGames.AttachAll(gameToPlayer);
            db.SubmitChanges();
            return true;
        }
        return false;
    }

    public bool AddPlayer(Player player)
    {

        try
        {

            if (db.TblPlayers.SingleOrDefault(p => p.Id == player.Id || p.Name == player.Name) != null)
            {
                return false;
            }
            else
            {
                if (player.Password == null || player.Name == null) // Can't create user without password or name
                { return false; }

                TblPlayer newTblPlayer = new TblPlayer { Name = player.Name, Password = player.Password };

                if (player.Family != null && player.Family.Id > 0)
                {
                    TblFamily familyFromDB = db.TblFamilies.SingleOrDefault(f => f.Id == player.Family.Id);
                    if (familyFromDB == null) // We didn't found family with this Id
                    { return false; }

                    // Remove Previous binding
                    var x = db.TblFamilyPlayers.SingleOrDefault(pf => pf.idPlayer == player.Id);
                    if (x != null)
                    {
                        db.TblFamilyPlayers.DeleteOnSubmit(x);
                    }
                    TblFamilyPlayer familyToPLayerBinding = new TblFamilyPlayer { idPlayer = player.Id, idFamily = player.Family.Id };
                    db.TblFamilyPlayers.Attach(familyToPLayerBinding);
                }
                db.TblPlayers.Attach(newTblPlayer);
                db.SubmitChanges();
            }
        }
        catch (Exception e) {  /* If we have any exception we just return false*/}
        return false;
    }

    public List<Game> GetGames()
    {
        var games =
            from g in db.TblGames
            select new Game { Id = g.Id, CreatedDateTime = g.CreatedDate };
        return games.ToList();
    }

    public List<Game> GetGamesByPlayerId(string playerId)
    {
        int id = Convert.ToInt32(playerId);
        var gamesFromDB =
            from g in db.TblGames
            join pg in db.TblPlayerGames on g.Id equals pg.idGame
            where pg.idPlayer == id
            select new Game { Id = g.Id, CreatedDateTime = g.CreatedDate };

        List<Game> games = gamesFromDB.ToList();

        for (int i = 0; i < games.Count(); i++)
        {
            List<Player> players = GetPlayersByGame(games[i].Id.ToString());
            games[i].Player1 = players.ElementAtOrDefault(0);
            games[i].Player2 = players.ElementAtOrDefault(1);
        }

        return games;
    }

    public List<Player> GetPlayersByGame(string gameId)
    {
        int id = Convert.ToInt32(gameId);
        var players =
            from p in db.TblPlayers
            join pg in db.TblPlayerGames on p.Id equals pg.idPlayer
            join pf in db.TblFamilyPlayers on pg.idPlayer equals pf.idPlayer
            join f in db.TblFamilies on pf.idFamily equals f.Id
            where pg.idGame == id
            select new Player { Id = p.Id, Name = p.Name, Family = new Family { Id = f.Id, Name = f.Name } };
        return players.ToList();
    }

    public List<Player> GetPlayers()
    {

        var players =
            from p in db.TblPlayers
            join pf in db.TblFamilyPlayers on p.Id equals pf.idPlayer
            join f in db.TblFamilies on pf.idFamily equals f.Id
            select new Player { Id = p.Id, Name = p.Name, Family = new Family { Id = f.Id, Name = f.Name } };
        return players.ToList();
    }

    public int GetTotalGamesCountForPlayer(string playerId)
    {
        int id = Convert.ToInt32(playerId);
        return db.TblPlayerGames.Where(pg => pg.idPlayer == id).Count();
    }

    public bool RemoveGame(string gameId)
    {
        int id = Convert.ToInt32(gameId);
        var x = db.TblGames.SingleOrDefault(g => g.Id == id);
        if (x != null)
        {
            db.TblGames.DeleteOnSubmit(x);
            db.SubmitChanges();
            return true;
        }
        return false;
    }

    public bool RemovePlayer(string playerId)
    {
        int id = Convert.ToInt32(playerId);
        var x = db.TblPlayerGames.SingleOrDefault(p => p.Id == id);
        if (x != null)
        {
            db.TblPlayerGames.DeleteOnSubmit(x);
            db.SubmitChanges();
            return true;
        }
        return false;
    }

    public bool UpdateGame(Game game)
    {
        // Update Game
        try
        {
            // The only thing that we can update in game it's player binding
            var gamesMetaInfo = db.TblPlayerGames.Where(pg => pg.idGame == game.Id);

            if (gamesMetaInfo.Count() == 0)
            {
                // We need to create connection between player and game

            }
            if (gamesMetaInfo.Count() > 1)
            {
                TblPlayerGame gameMetaInfo0 = gamesMetaInfo.ElementAt(0);
                TblPlayerGame gameMetaInfo1 = gamesMetaInfo.ElementAt(1);
                gameMetaInfo0.idPlayer = game.Player1.Id;
                gameMetaInfo1.idPlayer = game.Player2.Id;
                lock (DB_WRITE_LOCK)
                {
                    db.TblPlayerGames.Attach(gameMetaInfo0);
                    db.TblPlayerGames.Attach(gameMetaInfo1);
                    db.SubmitChanges();
                }
            }
            if (gamesMetaInfo.Count() > 0)
            {
                TblPlayerGame gameMetaInfo0 = gamesMetaInfo.ElementAt(0);
                gameMetaInfo0.idPlayer = game.Player1.Id;
                TblPlayerGame gameMetaInfo1 = new TblPlayerGame { idGame = game.Id, idPlayer = game.Player2.Id };
                lock(DB_WRITE_LOCK)
                {
                    db.TblPlayerGames.Attach(gameMetaInfo1);
                    db.SubmitChanges();
                }
            }
        }
        catch (Exception e) { /* If we have any exception we just return false*/}
        return false;
    }

    public bool UpdatePlayer(Player player)
    {
        try
        {
            var playerFromDb = db.TblPlayers.SingleOrDefault(p => p.Id == player.Id);

            playerFromDb.Name = player.Name;

            var familiesToPlayerFromDb = db.TblFamilyPlayers.Where(pf => pf.idPlayer == player.Id);

            if (familiesToPlayerFromDb.Count() == 0)
            {
                // New binding
                if (db.TblFamilies.Where(f => f.Id == player.Family.Id).Count() > 0)
                {
                    TblFamilyPlayer familyToPlayer4DB = new TblFamilyPlayer { idPlayer = player.Id, idFamily = player.Family.Id };
                    db.TblFamilyPlayers.Attach(familyToPlayer4DB);
                }
                else
                { // No such family. need first create one
                    return false;
                }
            }
            else // Changind player <=> family connection
            {
                TblFamilyPlayer familyToPlayerFromDb = familiesToPlayerFromDb.First();
                familyToPlayerFromDb.idFamily = player.Family.Id;
                db.TblFamilyPlayers.Attach(familyToPlayerFromDb);
            }

            db.SubmitChanges();
        }
        catch (Exception e) {  /* If we have any exception we just return false*/}
        return false;
    }

    public Player GetPlayerById(string playerId)
    {
        int id = Convert.ToInt32(playerId);
        var player =
            from p in db.TblPlayers
            join pf in db.TblFamilyPlayers on p.Id equals pf.idPlayer
            join f in db.TblFamilies on pf.idFamily equals f.Id
            where p.Id == id
            select new Player { Id = p.Id, Name = p.Name, Family = new Family { Id = f.Id, Name = f.Name } };
        return player.First();
    }

    public Game GetGameById(string gameId)
    {
        int id = Convert.ToInt32(gameId);
        var games =
            from g in db.TblGames
            join pg in db.TblPlayerGames on g.Id equals pg.idGame
            join p in db.TblPlayers on pg.idPlayer equals p.Id
            where g.Id == id
            select new Game { Id = g.Id, CreatedDateTime = g.CreatedDate };

        Game game = games.First();
        List<Player> players = GetPlayersByGame(game.Id.ToString());
        if (players.Count > 0)
        {
            game.Player1 = players.ElementAtOrDefault(0);
        }
        if (players.Count > 1)
        {
            game.Player1 = players.ElementAtOrDefault(1);
        }

        return game;
    }

    public bool AddFamily(Family family)
    {
        try
        {
            if (family == null || family.Name == null || db.TblFamilies.Where(f => f.Id == family.Id).Count() > 0)
            {
                return false;
            }
            else
            {
                TblFamily tblFamily = new TblFamily { Name = family.Name };
                db.TblFamilies.Attach(tblFamily);
            }
            db.SubmitChanges();
        }
        catch (Exception e) {  /* If we have any exception we just return false*/ }
        return false;
    }

    public bool DeleteFamily(string familyId)
    {
        try
        {
            int id = Convert.ToInt32(familyId);
            var x = db.TblFamilies.SingleOrDefault(p => p.Id == id);
            if (x != null)
            {
                db.TblFamilies.DeleteOnSubmit(x);
                db.SubmitChanges();
                return true;
            }
        }
        catch (Exception e) {  /* If we have any exception we just return false*/}
        return false;
    }

    public Family GetFamily(string familyId)
    {
        int id = Convert.ToInt32(familyId);
        var family =
            from f in db.TblFamilies
            where f.Id == id
            select new Family { Name = f.Name, Id = f.Id };
        return family.First();
    }

    public List<Player> GetPlayersByFamily(string familyId)
    {
        int id = Convert.ToInt32(familyId);
        var players =
            from p in db.TblPlayers
            join pf in db.TblFamilyPlayers on p.Id equals pf.idPlayer
            join f in db.TblFamilies on pf.idFamily equals f.Id
            where pf.idFamily == id
            select new Player { Id = p.Id, Name = p.Name, Family = new Family { Id = f.Id, Name = f.Name } };
        return players.ToList();
    }

    public List<Move> GetMovesByGame(string gameId)
    {
        int id = Convert.ToInt32(gameId);
        var moves =
            from m in db.TblMoves
            where m.idGame == id
            orderby m.Id ascending
            select new Move
            {
                Id = m.Id,
                DateTime = m.CreatedDate,
                From = new Coordinate { X = m.From_X, Y = m.From_Y },
                To = new Coordinate { X = m.To_X, Y = m.To_Y },
                PlayerId = m.idPlayer,
                GameId = m.idGame
            };
        return moves.ToList();
    }

    public void MakeMove(Move move)
    {
        IDuplexCheckersServiceCallback sessionCallback = OperationContext.Current.GetCallbackChannel<IDuplexCheckersServiceCallback>();

        Player player = GetPlayerById(move.PlayerId.ToString());
        IDuplexCheckersServiceCallback storedCallback = null;
        Status status = Status.WRONG_INPUT;
        List<Move> availableMoves = null;

        if (lookupPlayer2Callback.TryGetValue(player, out storedCallback))
        {    
            status = StoreMove(move, player, out availableMoves);
        }
        else
        {
            status = Status.NOT_LOGGED_IN;
        }
        sessionCallback.MakeMoveCallback(status);
    }

    private Status StoreMove(Move move, Player player, out List<Move> availableMoves)
    {
        Status status = Status.WRONG_INPUT;
        availableMoves = new List<Move>();
        try
        {
            Game game = GetGameById(move.GameId.ToString());

            TblMove tblMove = new TblMove {
                idPlayer = move.PlayerId,
                idGame = move.GameId,
                From_X = move.From.X,
                From_Y = move.From.Y,
                To_X = move.To.X,
                To_Y = move.To.Y,
                CreatedDate = move.DateTime
            };



            Player player2 = null;
            if (game.Player1.Equals(player)) { player2 = game.Player2; }
            else { player2 = game.Player1; }

            availableMoves = GetAvailableMoves(player, game, move);
            if (MoveIsValid(player, game, move) && availableMoves.Count > 0)
            {
                Move lastMove = GetMovesByGame(game.Id.ToString()).Last();
                lookupPlayer2Callback[player2].PlayerTurnCallback(lastMove);
                status = Status.MOVE_ACCEPTED;
            }
            else
            {
                status = Status.GAME_LOSE;

                
                lookupPlayer2Callback[player2].MakeMoveCallback(Status.GAME_WIN);
            }
        } catch (Exception e) { }

        return status;
    }

    private bool MoveIsValid(Player player, Game game, Move move)
    {
        Coordinate from = move.From;
        Coordinate to = move.To;
        Player player2 = game.Player1.Equals(player) ? game.Player2 : game.Player1;

        if (to.X > MAX_COLLS || to.X < 0 || to.Y > MAX_ROWS || to.Y < 0) return false;
        Coordinate delta = new Coordinate { X = to.X - from.X, Y = to.Y - from.Y };

        if (delta.X < 0 || delta.Y < 0) return false; // Jump outside the game plate
        if (delta.X > 2 || delta.Y > 2) return false; // Too long jump
        if (delta.X > 1 && delta.Y > 1) // we trying to eat somebody
        {
            // Get moves of other player in middle point
            Coordinate point = new Coordinate { };
            List<TblMove> moves = GetMovesByCoordinatesAndPlayer(point, player2);
            if (moves.Count > 0) // We actually eating some soldier
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        return false;
    }

    private List<TblMove> GetMovesByCoordinatesAndPlayer(Coordinate point, Player player)
    {
        var moves =
            from m in db.TblMoves
            where m.idPlayer == player.Id && (m.To_X == point.X && m.To_Y == point.Y)
            orderby m.Id ascending
            select m;
        return moves.ToList();
    }

    private List<Move> GetAvailableMoves(Player player, Game game, Move move)
    {
        throw new NotImplementedException();
    }

    public void Login(string name, string password)
    {
        IDuplexCheckersServiceCallback cb = OperationContext.Current.GetCallbackChannel<IDuplexCheckersServiceCallback>();
        Player player;
        List<Game> gameList= new List<Game>();
        Status status = validatePlayer(name, password, out player);

        if (Status.LOGIN_SUCCEDED == status)
        {
            // Cleanup old callbacks
            if (lookupPlayer2Callback.ContainsKey(player))
            {
                var callBack = lookupPlayer2Callback[player];
                if (lookupCallback2Player.ContainsKey(callBack))
                {
                    lookupCallback2Player.Remove(lookupPlayer2Callback[player]);
                }
                lookupPlayer2Callback.Remove(player);
            }
            lookupCallback2Player.Add(cb, player);
            lookupPlayer2Callback.Add(player, cb);
            gameList = GetGamesByPlayer(player);
        }

        cb.LoginCallback(player, gameList, status);
    }

    private Status validatePlayer(string name, string password, out Player player)
    {
        player = null;
        Status status = Status.LOGIN_SUCCEDED;

        if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(password))
        {
            status = Status.WRONG_INPUT;
        }
        else
        {
            try
            {
                var tblPlayer = db.TblPlayers.SingleOrDefault(p => p.Name == name && p.Password == password);
                player = GetPlayerById(tblPlayer.Id.ToString());
            }
            catch (Exception e)
            {
                status = Status.NO_SUCH_USER;
            }
        }
        return status;
    }

    public List<Move> RecoverGameMovesByPlayer(Game game, Player player)
    {
        List<Move> moves = GetMovesByGame(game.Id.ToString());
        var thisPlayerMoves =
            from m in moves
            where m.PlayerId == player.Id
            orderby m.Id ascending
            select m;
        return thisPlayerMoves.ToList();
    }

    public void StartGame(Game game, bool computerRival)
    {
        IDuplexCheckersServiceCallback cb = OperationContext.Current.GetCallbackChannel<IDuplexCheckersServiceCallback>();
        Status status = Status.GAME_STARTED;
        Player player = null;

        if (!lookupCallback2Player.TryGetValue(cb, out player))
        {
            status = Status.NOT_LOGGED_IN;
        }
        else
        {
            try
            {
                if (game != null) // Game exists
                {
                    if ((game.Player1 == null || game.Player1.Equals(player)) && game.Player2 != null && !game.Player2.Equals(player)) //Joining as Player1 or reconecting after a disctonnect
                    {
                        game.Player1 = player;
                        UpdateGame(game);
                        status = sendStartGameWakeupToSecondPlayer(game.Player2, game);
                    }
                    else if ((game.Player2 == null || game.Player2.Equals(player)) && game.Player1 != null && !game.Player1.Equals(player)) // Joining As Player2 or reconnection after disconnect
                    {
                        game.Player2 = player;
                        UpdateGame(game);
                        status = sendStartGameWakeupToSecondPlayer(game.Player1, game);
                    }
                    else
                    {
                        status = Status.GAME_ALREADY_STARTED_BY_OTHER_PLAYERS;
                        game = null;
                    }
                }
                else // game not exists
                {
                    status = Status.NO_SUCH_GAME;
                }
            }
            catch (Exception e)
            {
                status = Status.WRONG_INPUT;
            }
        }
        cb.StartGameCallback(game, status);
    }

    private Status sendStartGameWakeupToSecondPlayer(Player player, Game game)
    {
        Status status = Status.WAITING_FOR_OTHER_PLAYER_TO_ARRIVE;
        try
        {
            IDuplexCheckersServiceCallback cb = null;
            if (lookupPlayer2Callback.TryGetValue(player, out cb))
            {
                status = Status.GAME_STARTED;
                cb.StartGameCallback(game, status);
            }
        }
        catch (Exception e) { }
        return status;
    }

    public void SaveInitialPositions(List<Move> initialPositions, Status gameStatus)
    {
        if (initialPositions.Count > 0 && Status.GAME_STARTED == gameStatus)
        {
            List<TblMove> initialMoves = new List<TblMove>();
            foreach (var move in initialPositions)
            {
                initialMoves.Add(new TblMove
                {
                    CreatedDate = move.DateTime,
                    idPlayer = move.PlayerId,
                    idGame = move.GameId,
                    From_X = INITIAL_POINT.X,
                    From_Y = INITIAL_POINT.Y,
                    To_X = move.To.X,
                    To_Y = move.To.Y,
                });
                
            }

            lock (DB_WRITE_LOCK)
            {
                db.TblMoves.InsertAllOnSubmit(initialMoves);
                db.SubmitChanges();
            }

            Player player = GetPlayerById(initialPositions[0].PlayerId.ToString());
            Game game = GetGameById(initialPositions[0].GameId.ToString());
            if (game.Player1.Equals(player)) // Send client to start game if it's Player1 (Black)
            {
                Move move = GetMovesByGame(game.Id.ToString()).Last();
                lookupPlayer2Callback[player].PlayerTurnCallback(move);
            }
        }
    }

    public List<Game> GetGamesByPlayer(Player player)
    {
        return GetGamesByPlayerId(player.Id.ToString());
    }
}
