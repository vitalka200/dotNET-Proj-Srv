
GO
CREATE TABLE [dbo].[TblPlayer] (
    [Id]        INT          IDENTITY (1, 1) NOT NULL  PRIMARY KEY,
	[Password] VARCHAR (20) NOT NULL,
    [Name]      VARCHAR (20) NOT NULL
);

CREATE TABLE [dbo].[TblFamily] (
    [Id]   INT          IDENTITY (1, 1) NOT NULL  PRIMARY KEY,
    [Name] VARCHAR (20) NOT NULL
);

GO
CREATE TABLE [dbo].[TblGame] (
    [Id]          INT  IDENTITY (1, 1) NOT NULL PRIMARY KEY,
    [CreatedDate] DATETIME NOT NULL,
	[Status]      VARCHAR (255) DEFAULT ('NEW_GAME') NOT NULL,
	[WinnerPlayerNum] INT DEFAULT(0) CHECK (WinnerPlayerNum > -1 AND WinnerPlayerNum <3) NOT NULL
);

go
CREATE TABLE [dbo].[TblMove] (
    [Id]          INT  IDENTITY (1, 1) NOT NULL  PRIMARY KEY,
    [CreatedDate] DATETIME NOT NULL,
	[idPlayer]    INT  NOT NULL,
	[idGame]    INT  NOT NULL,
    [From_X]      INT  NOT NULL,
    [From_Y]      INT  NOT NULL,
    [To_X]        INT  NOT NULL,
    [To_Y]        INT  NOT NULL,
	[RivalEat]    BIT NOT NULL,
	CONSTRAINT [FK_MoPl_ToPl] FOREIGN KEY ([idPlayer]) REFERENCES [dbo].[TblPlayer] ([Id]),
	CONSTRAINT [FK_MoGa_ToGa] FOREIGN KEY ([idGame]) REFERENCES [dbo].[TblGame] ([Id]) ON DELETE CASCADE,
);




go

CREATE TABLE [dbo].[TblFamilyPlayer] (
    [Id]       INT IDENTITY (1, 1) NOT NULL  PRIMARY KEY,
    [idFamily] INT NOT NULL,
    [idPlayer] INT NOT NULL,
    CONSTRAINT [FK_FamPl_ToFam] FOREIGN KEY ([idFamily]) REFERENCES [dbo].[TblFamily] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FamPl_ToPl] FOREIGN KEY ([idPlayer]) REFERENCES [dbo].[TblPlayer] ([Id]) ON DELETE CASCADE
);

go
CREATE TABLE [dbo].[TblPlayerGame] (
    [Id]       INT IDENTITY (1, 1) NOT NULL  PRIMARY KEY,
    [idPlayer] INT NOT NULL,
    [idGame]   INT NOT NULL,
    CONSTRAINT [FK_PlGa_ToGa] FOREIGN KEY ([idGame]) REFERENCES [dbo].[TblGame] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PlGa_ToPl] FOREIGN KEY ([idPlayer]) REFERENCES [dbo].[TblPlayer] ([Id])
);