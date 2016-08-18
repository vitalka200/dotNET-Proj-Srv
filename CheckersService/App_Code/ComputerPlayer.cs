using LocalCheckersService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web;

namespace ComputerPlayer
{
    /// <summary>
    /// Summary description for ComputerPlayer
    /// </summary>
    public class ComputerPlayerImpl
    {

        private const int MAX_COLLS = 4;
        private const int MAX_ROWS = 8;
        private const int TOTAL_CHEKERS = 4;
        private LocalCheckersService.Coordinate INITIAL_POINT = new LocalCheckersService.Coordinate { X = -1, Y = -1 };
        public int[,] CheckersLocations = new int[MAX_ROWS,MAX_COLLS];
        public const int COMPUTER_PLAYER = 1;
        public const int REAL_PLAYER = 2;
        public Move LastMove { get; set; }
        public LocalCheckersService.DuplexCheckersServiceClient DuplexService { get; set; }
        public LocalCheckersService.SoapCheckersServiceClient SoapService { get; set; }
        public ComputerPlayerCheckerServiceHandler ServiceCallBackHandler { get; private set; }
        private const string COMPUTER_PLAYER_USERNAME = "Computer";
        private const string COMPUTER_PLAYER_PASSWORD = "password";
        public Player ComputerPlayerDTO { get; set; }
        public Game ComputerGameDTO { get; set; }
        public bool GameIsRunning { internal set; get; }
        private Random random = new Random();
        public Stack<Move> GeneratedMoves { get; private set; }


        public ComputerPlayerImpl()
        {
            ServiceCallBackHandler = new ComputerPlayerCheckerServiceHandler();
            ServiceCallBackHandler.PlayerImpl = this;
            DuplexService = new DuplexCheckersServiceClient(new System.ServiceModel.InstanceContext(ServiceCallBackHandler));
            SoapService = new SoapCheckersServiceClient();
            GeneratedMoves = new Stack<Move>();
            LoginComputerPlayer();
        }

        private void LoginComputerPlayer()
        {
            DuplexService.LoginAsync(COMPUTER_PLAYER_USERNAME, COMPUTER_PLAYER_PASSWORD);
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
            CheckersLocations[1,0] = COMPUTER_PLAYER; CheckersLocations[0,1] = COMPUTER_PLAYER;
            CheckersLocations[1,2] = COMPUTER_PLAYER; CheckersLocations[0,3] = COMPUTER_PLAYER; // Computer checkers
            CheckersLocations[7,0] = REAL_PLAYER; CheckersLocations[6,1] = REAL_PLAYER;
            CheckersLocations[7,2] = REAL_PLAYER; CheckersLocations[6,3] = REAL_PLAYER; // Computer checkers

            List<LocalCheckersService.Move> initialMoves = new List<LocalCheckersService.Move>();
            for (int i = 0; i < TOTAL_CHEKERS/2; i++)
            {
                Coordinate to1 = new Coordinate { X = 0, Y = i};
                Coordinate to2 = new Coordinate { X = i, Y = i};

                initialMoves.Add(new Move {
                    From = INITIAL_POINT,
                    To = to1,
                    PlayerId = ComputerPlayerDTO.Id,
                    DateTime = DateTime.Now,
                    GameId = ComputerGameDTO.Id
                });
                initialMoves.Add(new Move {
                    From = INITIAL_POINT,
                    To = to2,
                    PlayerId = ComputerPlayerDTO.Id,
                    DateTime = DateTime.Now,
                    GameId = ComputerGameDTO.Id
                });
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
            LastMove = GeneratedMoves.Pop();
            Debug.WriteLine("Sending generated move. {0}", LastMove);
            DuplexService.MakeMoveAsync(LastMove);
        }

        public void GenerateMoves()
        {
            Move move;
            bool wasEaten;
            for (int x = 0; x < CheckersLocations.GetLength(0); x++)
            {
                for (int y = 0; y < CheckersLocations.GetLength(1); y++)
                {
                    if (CheckersLocations[x,y] == COMPUTER_PLAYER)// Generate next move for checker
                    {
                        if (PointInBounds(x+1,y+1) && CheckersLocations[x+1,y+1] == REAL_PLAYER) // We'll eat player checker
                        {
                            move = new Move {
                                GameId = ComputerGameDTO.Id,
                                PlayerId = ComputerPlayerDTO.Id,
                                DateTime = DateTime.Now,
                                From = new Coordinate { X = x, Y = y},
                                To = new Coordinate { X = x+2, Y = y+2}
                            };
                            if (MoveIsValid(ComputerPlayerDTO, ComputerGameDTO, move)) { GeneratedMoves.Push(move); };
                        }
                        if (PointInBounds(x,y-1) && CheckersLocations[x,y-1] == REAL_PLAYER) // We'll eat player checker
                        {
                            move = new Move {
                                GameId = ComputerGameDTO.Id,
                                PlayerId = ComputerPlayerDTO.Id,
                                DateTime = DateTime.Now,
                                From = new Coordinate { X =x, Y =y},
                                To = new Coordinate { X = x+1, Y = y+1}
                            };
                            if (MoveIsValid(ComputerPlayerDTO, ComputerGameDTO, move)) { GeneratedMoves.Push(move); };
                        }
                        if (PointInBounds(x+1,y+1) && CheckersLocations[x+1,y+1] == 0)
                        {
                            move = new Move {
                                GameId = ComputerGameDTO.Id,
                                PlayerId = ComputerPlayerDTO.Id,
                                DateTime = DateTime.Now,
                                From = new Coordinate { X = x, Y = y},
                                To = new Coordinate { X = x+1, Y = y+1}
                            };
                            if (MoveIsValid(ComputerPlayerDTO, ComputerGameDTO, move)) { GeneratedMoves.Push(move); };
                        }
                    }
                }
            }
        }

        private bool PointInBounds(int x, int y)
        {
            if (x > MAX_ROWS-1 || x < 0) return false;
            if (y > MAX_COLLS-1 || y < 0) return false;
            return true;
        }

        private void GenerateMoreMoves()
        {
            GeneratedMoves.Clear();
            GenerateMoves();
        }
        private bool MoveIsValid(Player player, Game game, Move move)
        {
            Coordinate from = move.From;
            Coordinate to = move.To;
            Player player2 = game.Player1.Equals(player) ? game.Player2 : game.Player1;

            if (to.X > MAX_ROWS - 1 || to.X < 0 || to.Y > MAX_COLLS - 1 || to.Y < 0) return false;
            Coordinate delta = new Coordinate { X = to.X - from.X, Y = Math.Abs(to.Y - from.Y) };

            if (delta.X < 0 && game.Player2.Equals(player)) return false; // we black and going back. that not allowed
            if (delta.X > 0 && game.Player1.Equals(player)) return false; // we white and going back. that not allowed

            if (delta.X > 2 || delta.Y > 2) return false; // Too long jump
            return true;
        }

    }


    public class ComputerPlayerCheckerServiceHandler : LocalCheckersService.IDuplexCheckersServiceCallback
    {
        public ComputerPlayerImpl PlayerImpl { get; set; }
        
        public void GameEnd(LocalCheckersService.Move lastRivalMove, LocalCheckersService.Status status)
        {
            PlayerImpl.GameIsRunning = false;
            Debug.WriteLine("Game Was ended. My Status: {0}", status.ToString());
        }

        public void LoginCallback(LocalCheckersService.Player player, LocalCheckersService.Game[] playerGames, LocalCheckersService.Status status)
        {
            PlayerImpl.ComputerPlayerDTO = player;
            Debug.WriteLine("Computer player : {0} logedIn. status: {1}", (Player)player, status.ToString());
        }

        public void MakeMoveCallback(LocalCheckersService.Status status)
        {
            while (LocalCheckersService.Status.MOVE_ACCEPTED != status && 
                LocalCheckersService.Status.GAME_LOSE != status && 
                LocalCheckersService.Status.GAME_WIN != status)
            {
                Debug.WriteLine("Move wasn't accepted. Sending new one.");
                PlayerImpl.SendAdditionalMove();

            }
            if (LocalCheckersService.Status.MOVE_ACCEPTED == status)
            {
                PlayerImpl.CheckersLocations[PlayerImpl.LastMove.To.X, PlayerImpl.LastMove.To.Y] = ComputerPlayerImpl.COMPUTER_PLAYER;
                PlayerImpl.CheckersLocations[PlayerImpl.LastMove.From.X, PlayerImpl.LastMove.From.Y] = 0;
            }
            Debug.WriteLine("ComputerMove status: " + status.ToString());
        }

        public void PlayerTurnCallback(LocalCheckersService.Move lastRivalMove)
        {
            Debug.WriteLine("Computer Player got turn. Last rival move: {0}", (Move)lastRivalMove);
            PlayerImpl.CheckersLocations[lastRivalMove.To.X, lastRivalMove.To.Y] = ComputerPlayerImpl.REAL_PLAYER;
            PlayerImpl.CheckersLocations[lastRivalMove.From.X, lastRivalMove.From.Y] = 0;
            if (Math.Abs(lastRivalMove.To.X - lastRivalMove.From.X) > 1)
            {
                int deltaColl = (lastRivalMove.To.Y > lastRivalMove.From.Y) ? 1 : -1; // Get coordinate of assumed rival

                Coordinate point = new Coordinate { X = lastRivalMove.From.X + 1, Y = lastRivalMove.From.Y + deltaColl };

                PlayerImpl.CheckersLocations[point.X, point.Y] = 0;

            }
            PlayerImpl.MakeMove();
        }

        public void StartGameCallback(LocalCheckersService.Game game, LocalCheckersService.Status status)
        {
            PlayerImpl.GameIsRunning = true;
            Debug.WriteLine("Game: {0}. status: {1}", (Game)game, status.ToString());
        }
    }


}