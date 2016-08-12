using LocalCheckersService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace ComputerPlayer
{
    /// <summary>
    /// Summary description for ComputerPlayer
    /// </summary>
    public class ComputerPlayer
    {

        private const int MAX_COLLS = 4;
        private const int MAX_ROWS = 8;
        private const int TOTAL_CHEKERS = 4;
        private LocalCheckersService.Coordinate INITIAL_POINT = new LocalCheckersService.Coordinate { X = -1, Y = -1 };

        public LocalCheckersService.DuplexCheckersServiceClient DuplexService { get; set; }
        public LocalCheckersService.SoapCheckersServiceClient SoapService { get; set; }
        public ComputerPlayerCheckerServiceHandler ServiceCallBackHandler { get; private set; }
        private const string COMPUTER_PLAYER_USERNAME = "ComputerPlayer";
        private const string COMPUTER_PLAYER_PASSWORD = "password";
        public Player ComputerPlayerDTO { get; set; }
        public Game ComputerGameDTO { get; set; }
        public bool GameIsRunning { internal set; get; }
        private Random random = new Random();
        public Stack<Move> GeneratedMoves { get; private set; }


        public ComputerPlayer()
        {
            ServiceCallBackHandler = new ComputerPlayerCheckerServiceHandler();
            ServiceCallBackHandler.ComputerPlayer = this;
            DuplexService = new DuplexCheckersServiceClient(new System.ServiceModel.InstanceContext(ServiceCallBackHandler));
            SoapService = new SoapCheckersServiceClient();
            GeneratedMoves = new Stack<Move>();
            LoginComputerPlayer();
        }

        private void LoginComputerPlayer()
        {
            DuplexService.Login(COMPUTER_PLAYER_USERNAME, COMPUTER_PLAYER_PASSWORD);
        }

        public void StartGameWithComputerPlayer(Game game)
        {
            game.Player2 = ComputerPlayerDTO;
            ComputerGameDTO = game;
            DuplexService.StartGame(game, false);
            DuplexService.SaveInitialPositions(GenerateInitialPositions(), LocalCheckersService.Status.GAME_STARTED);
        }

        public LocalCheckersService.Move[] GenerateInitialPositions()
        {
            List<LocalCheckersService.Move> initialMoves = new List<LocalCheckersService.Move>();
            for (int i = 0; i < TOTAL_CHEKERS/2; i++)
            {
                Coordinate to1 = new Coordinate { X = 0, Y = i};
                Coordinate to2 = new Coordinate { X = i, Y = i};
                initialMoves.Add(new Move { From = INITIAL_POINT, To = to1, PlayerId = ComputerPlayerDTO.Id, DateTime = DateTime.Now, GameId = ComputerGameDTO.Id});
                initialMoves.Add(new Move { From = INITIAL_POINT, To = to2, PlayerId = ComputerPlayerDTO.Id, DateTime = DateTime.Now, GameId = ComputerGameDTO.Id });
            }
            return initialMoves.ToArray();
        }

        public void MakeMove()
        {
            GenerateMoreMoves();
            SendAdditionalMove();
        }

        public void SendAdditionalMove()
        {
            Move move = GeneratedMoves.Pop();
            Console.WriteLine("Sending generated move. {0}", move);
            DuplexService.MakeMove(move);
        }

        public void GenerateMoves(Coordinate pointFrom)
        {
            for (int i = 1; i < 3; i++)
            { 
                GeneratedMoves.Push(new Move {
                    GameId = ComputerGameDTO.Id,
                    PlayerId = ComputerPlayerDTO.Id,
                    DateTime = DateTime.Now,
                    From = pointFrom,
                    To = new Coordinate { X = pointFrom.X + i, Y = pointFrom.Y + i  }
                });
                GeneratedMoves.Push(new Move {
                    GameId = ComputerGameDTO.Id,
                    PlayerId = ComputerPlayerDTO.Id,
                    DateTime = DateTime.Now,
                    From = pointFrom,
                    To = new Coordinate { X = pointFrom.X + i, Y = pointFrom.Y - i  }
                });
                GeneratedMoves.Push(new Move {
                    GameId = ComputerGameDTO.Id,
                    PlayerId = ComputerPlayerDTO.Id,
                    DateTime = DateTime.Now,
                    From = pointFrom,
                    To = new Coordinate { X = pointFrom.X - i, Y = pointFrom.Y + i  }
                });
                GeneratedMoves.Push(new Move {
                    GameId = ComputerGameDTO.Id,
                    PlayerId = ComputerPlayerDTO.Id,
                    DateTime = DateTime.Now,
                    From = pointFrom,
                    To = new Coordinate { X = pointFrom.X - i, Y = pointFrom.Y - i  }
                });
            }
        }

        private void GenerateMoreMoves()
        {
            GeneratedMoves.Clear();

            List<LocalCheckersService.Move> allMoves = SoapService.RecoverGameMovesByPlayer(ComputerGameDTO, ComputerPlayerDTO).ToList();
            foreach (var move in allMoves)
            {
                GenerateMoves(move.To);
            }
        }

    }


    public class ComputerPlayerCheckerServiceHandler : LocalCheckersService.IDuplexCheckersServiceCallback
    {
        public ComputerPlayer ComputerPlayer { get; set; }
        
        public void GameEnd(LocalCheckersService.Move lastRivalMove, LocalCheckersService.Status status)
        {
            ComputerPlayer.GameIsRunning = false;
            Console.WriteLine("Game Was ended. My Status: {0}", status.ToString());
        }

        public void LoginCallback(LocalCheckersService.Player player, LocalCheckersService.Game[] playerGames, LocalCheckersService.Status status)
        {
            ComputerPlayer.ComputerPlayerDTO = player;
            Console.WriteLine("Computer player : {0} logedIn. status: {1}", (Player)player, status.ToString());
        }

        private void GetLast4Moves()
        {
            List<LocalCheckersService.Move> lastMoves = ComputerPlayer.SoapService.RecoverGameMovesByPlayer(ComputerPlayer.ComputerGameDTO, ComputerPlayer.ComputerPlayerDTO).ToList();
            List<LocalCheckersService.Move> last4Moves = lastMoves.GetRange(lastMoves.Count - 4, 4);
        }

        public void MakeMoveCallback(LocalCheckersService.Status status)
        {
            while (LocalCheckersService.Status.MOVE_ACCEPTED != status)
            {
                Console.WriteLine("Move wasn't accepted. Sending new one.");
                ComputerPlayer.SendAdditionalMove();

            }
            Console.WriteLine("ComputerMove status: {0}", status.ToString());
        }

        public void PlayerTurnCallback(LocalCheckersService.Move lastRivalMove)
        {
            Console.WriteLine("Computer Player got turn. Last rival move: {0}", (Move)lastRivalMove);
            ComputerPlayer.MakeMove();
        }

        public void StartGameCallback(LocalCheckersService.Game game, LocalCheckersService.Status status)
        {
            ComputerPlayer.GameIsRunning = true;
            Console.WriteLine("Game: {0}. status: {1}", (Game)game, status.ToString());
        }
    }


}