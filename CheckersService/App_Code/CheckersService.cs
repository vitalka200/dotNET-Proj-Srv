using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;

[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
public class CheckersService : IRestCheckersService, IDuplexCheckersService, ISoapCheckersService // Interfaces
{
    private const int MAX_COLLS = 4;
    private const int MAX_ROWS = 8;
    private const int TOTAL_CHEKERS = 4;
    private const int COMPUTER_USER_ID = 5;
    private Player ComputerPlayer { get; set; }

    private const int NO_WINNER = 0;
    private const int FIRST_WON = 1;
    private const int SECOND_WON = 2;
    private Coordinate INITIAL_POINT = new Coordinate { X = -1, Y = -1};

    private Dictionary<int, IDuplexCheckersServiceCallback> lookupPlayer2Callback = new Dictionary<int, IDuplexCheckersServiceCallback>();
    private Dictionary<IDuplexCheckersServiceCallback, Player> lookupCallback2Player = new Dictionary<IDuplexCheckersServiceCallback, Player>();

    public CheckersService()
    {
        ComputerPlayer = GetPlayerById(COMPUTER_USER_ID.ToString());
    }

    public bool AddGame(Game game)
    {
        if (game != null && game.Player1 != null && game.Player2 != null)
        {
            CreateNewGame(game);
            return true;
        }
        return false;
    }

    public Game CreateNewGame(Game game)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
        TblGame gameToSave = new TblGame { CreatedDate = game.CreatedDateTime , Status = Status.NEW_GAME.ToString()};
        db.TblGames.InsertOnSubmit(gameToSave);
        db.SubmitChanges();

        gameToSave = db.TblGames.Where(g => g.CreatedDate == game.CreatedDateTime).First();

        try
        {
            TblPlayerGame[] gameToPlayer = {
                new TblPlayerGame { idGame = gameToSave.Id, idPlayer = game.Player1.Id },
                new TblPlayerGame { idGame = gameToSave.Id, idPlayer = game.Player2.Id }
            };

            db.TblPlayerGames.InsertAllOnSubmit(gameToPlayer);
            db.SubmitChanges();
        } catch (Exception e)
        {
            Debug.WriteLine("CreateNewGame(). Something went wrong..." + e.Message);
        }

        return new Game {
            Id = gameToSave.Id,
            Player1 = game.Player1,
            Player2 = game.Player2,
            CreatedDateTime = gameToSave.CreatedDate,
            GameStatus = (Status)Enum.Parse(typeof(Status), gameToSave.Status, true),
            WinnerPlayerNum = gameToSave.WinnerPlayerNum
        };
    }

    public bool AddPlayer(Player player)
    {

        try
        {
            CheckersDBDataContext db = new CheckersDBDataContext();
            if (db.TblPlayers.SingleOrDefault(p => p.Name == player.Name) != null)
            {
                return false;
            }
            else
            {
                if (player.Password == null || player.Name == null) // Can't create user without password or name
                { return false; }

                TblPlayer newTblPlayer = new TblPlayer { Name = player.Name, Password = player.Password };
                db.TblPlayers.InsertOnSubmit(newTblPlayer);
                db.SubmitChanges();

                newTblPlayer = db.TblPlayers.Where(p=>p.Name == player.Name && p.Password == player.Password).First();

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
                    db.TblFamilyPlayers.InsertOnSubmit(familyToPLayerBinding);
                    db.SubmitChanges();
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        return false;
    }

    public List<Game> GetGames()
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
        var gamesFromDB =
            from g in db.TblGames
            select new Game {
                Id = g.Id,
                CreatedDateTime = g.CreatedDate,
                GameStatus = (Status)Enum.Parse(typeof(Status), g.Status, true),
                WinnerPlayerNum = g.WinnerPlayerNum
            };

        List<Game> games = gamesFromDB.ToList();

        for (int i = 0; i < games.Count(); i++)
        {
            List<Player> players = GetPlayersByGame(games[i].Id.ToString());
            if (players.Count > 0)
            {
                games[i].Player1 = players.ElementAtOrDefault(0);
            }
            if (players.Count > 1)
            {
                games[i].Player2 = players.ElementAtOrDefault(1);
            }
        }

        var result =
            from g in games
            where // We don't want to show computer games
                    !ComputerPlayer.Equals(g.Player1) && !ComputerPlayer.Equals(g.Player2)
            select g;

        return result.ToList();
    }

    public List<Game> GetGamesByPlayerId(string playerId)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
        int id = Convert.ToInt32(playerId);
        var gamesFromDB =
            from g in db.TblGames
            join pg in db.TblPlayerGames on g.Id equals pg.idGame
            where pg.idPlayer == id
            select new Game {
                Id = g.Id,
                CreatedDateTime = g.CreatedDate,
                GameStatus = (Status)Enum.Parse(typeof(Status), g.Status, true),
                WinnerPlayerNum = g.WinnerPlayerNum
            };

        List<Game> games = gamesFromDB.ToList();

        for (int i = 0; i < games.Count(); i++)
        {
            List<Player> players = GetPlayersByGame(games[i].Id.ToString());
            games[i].Player1 = players.ElementAtOrDefault(0);
            games[i].Player2 = players.ElementAtOrDefault(1);
        }

        var result =
            from g in games
            where // We don't want to show computer games
                    !ComputerPlayer.Equals(g.Player1) && !ComputerPlayer.Equals(g.Player2)
            select g;

        return result.ToList();
    }

    public List<Player> GetPlayersByGame(string gameId)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
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
        CheckersDBDataContext db = new CheckersDBDataContext();
        var players =
            from p in db.TblPlayers
            join pf in db.TblFamilyPlayers on p.Id equals pf.idPlayer
            join f in db.TblFamilies on pf.idFamily equals f.Id
            select new Player { Id = p.Id, Name = p.Name, Family = new Family { Id = f.Id, Name = f.Name } };
        return players.ToList();
    }

    public int GetTotalGamesCountForPlayer(string playerId)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
        int id = Convert.ToInt32(playerId);
        return db.TblPlayerGames.Where(pg => pg.idPlayer == id).Count();
    }

    public bool RemoveGame(string gameId)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
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
        CheckersDBDataContext db = new CheckersDBDataContext();
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
            CheckersDBDataContext db = new CheckersDBDataContext();
            // The only thing that we can update in game it's player binding
            int counter = 0;
            var gamesMetaInfo = db.TblPlayerGames.Where(pg => pg.idGame == game.Id);
            var gameFromDb = db.TblGames.Where(g => g.Id == game.Id).First();

            if (!gameFromDb.Status.Equals(game.GameStatus)) // Save game status if changed game status
            {
                gameFromDb.Status = game.GameStatus.ToString();
                db.SubmitChanges();
            }

            if (gamesMetaInfo.Count() > 1) // we have at least 2 records
            {
                foreach (var gameMetaInfo in gamesMetaInfo)
                {
                    if (counter == 0) { gameMetaInfo.idPlayer = game.Player1.Id; }// first element
                    else if (counter == 1) { gameMetaInfo.idPlayer = game.Player2.Id; } // second
                    else { break; }
                }
                db.SubmitChanges();
            }
            else if (gamesMetaInfo.Count() > 0) // we have exactly 1 record
            {
                TblPlayerGame gameMetaInfo0 = gamesMetaInfo.First();
                gameMetaInfo0.idPlayer = game.Player1.Id;
                TblPlayerGame gameMetaInfo1 = new TblPlayerGame { idGame = game.Id, idPlayer = game.Player2.Id };
                db.TblPlayerGames.InsertOnSubmit(gameMetaInfo1);
                db.SubmitChanges();
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        return false;
    }

    public bool UpdatePlayer(Player player)
    {
        try
        {
            CheckersDBDataContext db = new CheckersDBDataContext();
            var playerFromDb = db.TblPlayers.SingleOrDefault(p => p.Id == player.Id);

            playerFromDb.Name = player.Name;

            var familiesToPlayerFromDb = db.TblFamilyPlayers.Where(pf => pf.idPlayer == player.Id);

            if (familiesToPlayerFromDb.Count() == 0)
            {
                // New binding
                if (db.TblFamilies.Where(f => f.Id == player.Family.Id).Count() > 0)
                {
                    TblFamilyPlayer familyToPlayer4DB = new TblFamilyPlayer { idPlayer = player.Id, idFamily = player.Family.Id };
                    db.TblFamilyPlayers.InsertOnSubmit(familyToPlayer4DB);
                    db.SubmitChanges();
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
                db.SubmitChanges();
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        return false;
    }

    public Player GetPlayerById(string playerId)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
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
        CheckersDBDataContext db = new CheckersDBDataContext();
        int id = Convert.ToInt32(gameId);
        var games =
            from g in db.TblGames
            join pg in db.TblPlayerGames on g.Id equals pg.idGame
            join p in db.TblPlayers on pg.idPlayer equals p.Id
            where g.Id == id
            select new Game {
                Id = g.Id,
                CreatedDateTime = g.CreatedDate,
                GameStatus = (Status)Enum.Parse(typeof(Status), g.Status, true),
                WinnerPlayerNum = g.WinnerPlayerNum
            };

        Game game = games.First();
        List<Player> players = GetPlayersByGame(game.Id.ToString());
        if (players.Count > 0)
        {
            game.Player1 = players.ElementAtOrDefault(0);
        }
        if (players.Count > 1)
        {
            game.Player2 = players.ElementAtOrDefault(1);
        }

        return game;
    }

    public bool AddFamily(Family family)
    {
        try
        {
            if (family != null || family.Name != null)
            {
                CheckersDBDataContext db = new CheckersDBDataContext();
                var familyFromDb = db.TblFamilies.SingleOrDefault(f => f.Name == family.Name);
                //we already have this family
                if (familyFromDb != null) { return false; }
                else
                {
                    db.TblFamilies.InsertOnSubmit(new TblFamily { Name = family.Name });
                    db.SubmitChanges();
                }

                return true;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        return false;
    }

    public bool UpdateFamily(Family family)
    {
        if (family != null)
        {
            CheckersDBDataContext db = new CheckersDBDataContext();
            var familyFromDb = db.TblFamilies.SingleOrDefault(f => f.Name == family.Name);
            //we don't have this family
            if (familyFromDb == null) { return false; }
            else
            {
                familyFromDb.Name = family.Name;
                db.SubmitChanges();
            }
            return true;
        }
        return false;
    }

    public bool DeleteFamily(string familyId)
    {
        try
        {
            CheckersDBDataContext db = new CheckersDBDataContext();
            int id = Convert.ToInt32(familyId);
            var x = db.TblFamilies.SingleOrDefault(p => p.Id == id);
            if (x != null)
            {
                db.TblFamilies.DeleteOnSubmit(x);
                db.SubmitChanges();
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        return false;
    }

    public Family GetFamily(string familyId)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
        int id = Convert.ToInt32(familyId);
        var family =
            from f in db.TblFamilies
            where f.Id == id
            select new Family { Name = f.Name, Id = f.Id };
        return family.First();
    }

    public List<Player> GetPlayersByFamily(string familyId)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
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
        CheckersDBDataContext db = new CheckersDBDataContext();
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

        if (lookupPlayer2Callback.TryGetValue(player.Id, out storedCallback)) { status = StoreMove(move, player);  }
        else { status = Status.NOT_LOGGED_IN; }

        if (Status.GAME_WIN == status || Status.GAME_LOSE == status)
        {
            Game game = GetGameById(move.GameId.ToString());
            sessionCallback.GameEnd(game, move, status);
            game.GameStatus = Status.GAME_COMPLETED;
            UpdateGame(game);
        }
        else
        {
            sessionCallback.MakeMoveCallback(status);
        }

    }

    private Status StoreMove(Move move, Player player)
    {
        Status status = Status.WRONG_INPUT;
        try
        {
            CheckersDBDataContext db = new CheckersDBDataContext();
            Game game = GetGameById(move.GameId.ToString());

            Player player2 = game.Player1.Equals(player) ? game.Player2 : game.Player1;
            

            bool wasEaten = false;
            bool isMoveValid = MoveIsValid(player, game, move, out wasEaten);
            bool reachedEnd = ReachedEndOfBoard(game, player, move);
            bool noMoreRivalChekers = GetEatenRivalCheckersByGame(game, player) >= TOTAL_CHEKERS;

            if (isMoveValid)
            {
                TblMove tblMove = new TblMove {
                    idPlayer = move.PlayerId,
                    idGame = move.GameId,
                    From_X = move.From.X,
                    From_Y = move.From.Y,
                    To_X = move.To.X,
                    To_Y = move.To.Y,
                    CreatedDate = move.DateTime,
                    RivalEat = wasEaten
                };
                status = Status.MOVE_ACCEPTED;
                db.TblMoves.InsertOnSubmit(tblMove);
                db.SubmitChanges();

                lookupPlayer2Callback[player2.Id].PlayerTurnCallback(move);
            }
            if (isMoveValid && reachedEnd)//If reached end, game won
            {
                status = Status.GAME_WIN;
                game.GameStatus = Status.GAME_COMPLETED;
                game.WinnerPlayerNum = game.Player1.Equals(player) ? 1 : 2; // Player 1 won the game
                UpdateGame(game);
                lookupPlayer2Callback[player2.Id].GameEnd(game, move, Status.GAME_LOSE);
            }
            else if (isMoveValid && wasEaten && (noMoreRivalChekers || reachedEnd))
            {
                status = Status.GAME_LOSE;
                game.GameStatus = Status.GAME_COMPLETED;
                game.WinnerPlayerNum = game.Player1.Equals(player) ? 2 : 1; // Player 2 won the game
                UpdateGame(game);
                lookupPlayer2Callback[player2.Id].GameEnd(game, move, Status.GAME_WIN);
            }
        } catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }

        return status;
    }

    private bool ReachedEndOfBoard(Game game, Player player, Move move)
    {
        List<Move> moves = RecoverGameMovesByPlayer(game, player);

        if (moves.Count < 1) return false; // Game wasn't started
        Move firstMove = moves.First();

        if (move.To.X > move.From.X) // We are white
        {
            return move.To.X == MAX_ROWS-1;
        }
        else // we are black
        {
            return move.To.X == 0;
        }
    }

    private int GetEatenRivalCheckersByGame(Game game, Player player)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
        Player player2 = game.Player1.Equals(player) ? game.Player2 : game.Player1;
        var eatMoves =
            from m in db.TblMoves
            where m.RivalEat == true && m.idPlayer == player2.Id
            select m;

        return eatMoves.Count();
    }

    public bool MoveIsValid(Player player, Game game, Move move, out bool wasEaten)
    {
        Coordinate from = move.From;
        Coordinate to = move.To;
        Player player2 = game.Player1.Equals(player) ? game.Player2 : game.Player1;
        wasEaten = false;

        if (to.X > MAX_ROWS-1 || to.X < 0 || to.Y > MAX_COLLS-1 || to.Y < 0) return false;
        Coordinate delta = new Coordinate { X = to.X - from.X, Y = Math.Abs(to.Y - from.Y) };

        if (delta.X < 0 && game.Player2.Equals(player)) return false; // we black and going back. that not allowed
        if (delta.X > 0 && game.Player1.Equals(player)) return false; // we white and going back. that not allowed

        if (delta.X > 2 || delta.Y > 2) return false; // Too long jump
        if (delta.X > 1 && delta.Y > 1) // we trying to eat somebody
        {
            // Get moves of other player in middle point
            int deltaColl = (to.Y > from.Y) ? 1 : -1; // Get coordinate of assumed rival

            //Coordinate point = new Coordinate { X = from.X + 1, Y = from.Y + deltaColl };
            Coordinate point = GetEatenCoordinateAndUpdate(move);
            List<TblMove> moves = GetMovesByCoordinatesAndPlayer(point, player2);
            if (moves.Count > 0) // We actually eating some soldier
            {
                wasEaten = true;
                return true;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private Coordinate GetEatenCoordinateAndUpdate(Move move)
    {
        int deltaColl = (move.To.Y > move.From.Y) ? 1 : -1; // Get coordinate of assumed rival
        int newX = move.From.X + deltaColl;
        int newY = move.From.Y + deltaColl;
        Coordinate point = new Coordinate {
            X = newX > 0 && newX < MAX_ROWS ? newX : move.To.X - deltaColl,
            Y = newY > 0 && newY < MAX_COLLS ? newY : move.From.Y - deltaColl
        };
        return point;
    }

    private List<TblMove> GetMovesByCoordinatesAndPlayer(Coordinate point, Player player)
    {
        CheckersDBDataContext db = new CheckersDBDataContext();
        var moves =
            from m in db.TblMoves
            where m.idPlayer == player.Id && (m.To_X == point.X && m.To_Y == point.Y)
            orderby m.Id ascending
            select m;
        return moves.ToList();
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
            if (lookupPlayer2Callback.ContainsKey(player.Id))
            {
                var callBack = lookupPlayer2Callback[player.Id];
                if (lookupCallback2Player.ContainsKey(callBack))
                {
                    lookupCallback2Player.Remove(lookupPlayer2Callback[player.Id]);
                }
                lookupPlayer2Callback.Remove(player.Id);
            }
            lookupCallback2Player.Add(cb, player);
            lookupPlayer2Callback.Add(player.Id, cb);
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
                CheckersDBDataContext db = new CheckersDBDataContext();
                var tblPlayer = db.TblPlayers.SingleOrDefault(p => p.Name == name && p.Password == password);
                player = GetPlayerById(tblPlayer.Id.ToString());
            }
            catch (Exception)
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

    public List<Move> RecoverGameMovesByGameId(int gameId)
    {
        return GetMovesByGame(gameId.ToString());
    }

    public void StartGame(Game game, bool computerRival)
    {
        IDuplexCheckersServiceCallback cb = OperationContext.Current.GetCallbackChannel<IDuplexCheckersServiceCallback>();
        Status status = Status.GAME_STARTED;
        Player player = null;

        if (computerRival)
        {
            ComputerPlayer.ComputerPlayerImpl PlayerImpl = new ComputerPlayer.ComputerPlayerImpl();
            game = CreateNewGame(new Game { Player1 = game.Player1, Player2 = ComputerPlayer, CreatedDateTime = game.CreatedDateTime });
            game.GameStatus = Status.GAME_STARTED;
            PlayerImpl.ComputerPlayerDTO = ComputerPlayer;
            PlayerImpl.StartGameWithComputerPlayer(game);

            Thread computerPlayerThread = new Thread(new ParameterizedThreadStart((_) => {
                while (PlayerImpl.GameIsRunning)
                {
                    Thread.Sleep(1000);
                };
            }));
            computerPlayerThread.Start();
        }

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
                    if ((player.Equals(game.Player1) || game.Player1 == null) && game.Player2 != null) //Joining as Player1 or reconecting after a disctonnect
                    {
                        game.Player1 = player;
                        status = sendStartGameWakeupToSecondPlayer(game.Player2, game);
                    }
                    else if ((player.Equals(game.Player2) || game.Player2 == null) && game.Player1 != null) // Joining As Player2 or reconnection after disconnect
                    {
                        game.Player2 = player;
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
            catch (Exception)
            {
                status = Status.WRONG_INPUT;
            }
        }
        if (Status.GAME_STARTED == status)
        {
            UpdateGame(game);
        }
        cb.StartGameCallback(game, status);
    }

    private Status sendStartGameWakeupToSecondPlayer(Player player, Game game)
    {
        Status status = Status.WAITING_FOR_OTHER_PLAYER_TO_ARRIVE;
        try
        {
            IDuplexCheckersServiceCallback cb = null;
            if (lookupPlayer2Callback.TryGetValue(player.Id, out cb))
            {
                status = Status.GAME_STARTED;
                cb.StartGameCallback(game, status);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("sendStartGameWakeup", e.Message);
        }
        return status;
    }

    public void SaveInitialPositions(List<Move> initialPositions, Status gameStatus)
    {
        if (initialPositions.Count > 0 && Status.GAME_STARTED == gameStatus)
        {
            CheckersDBDataContext db = new CheckersDBDataContext();
            
            foreach (var move in initialPositions)
            {
                TblMove tblMove = new TblMove {
                    CreatedDate = move.DateTime,
                    idPlayer = move.PlayerId,
                    idGame = move.GameId,
                    From_X = INITIAL_POINT.X,
                    From_Y = INITIAL_POINT.Y,
                    To_X = move.To.X,
                    To_Y = move.To.Y,
                };

                db.TblMoves.InsertOnSubmit(tblMove);
                db.SubmitChanges();
                
            }

            Player player = GetPlayerById(initialPositions[0].PlayerId.ToString());
            Game game = GetGameById(initialPositions[0].GameId.ToString());
            if (game.Player1.Equals(player)) // Send client to start game if it's Player1 (Black)
            {
                Move move = GetMovesByGame(game.Id.ToString()).Last();
                lookupPlayer2Callback[player.Id].PlayerTurnCallback(move);
            }
        }
    }

    public List<Game> GetGamesByPlayer(Player player)
    {
        return GetGamesByPlayerId(player.Id.ToString());
    }

    public Player LoginWeb(string name, string password)
    {
        Player player;
        List<Game> gameList = new List<Game>();
        Status status = validatePlayer(name, password, out player);
        return player;
    }
}
