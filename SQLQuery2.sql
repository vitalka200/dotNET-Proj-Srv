

CREATE TABLE [dbo].[TblFamily] (
    [Id]   INT          IDENTITY (1, 1) NOT NULL,
    [Name] VARCHAR (20) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
CREATE TABLE [dbo].[TblGame] (
    [Id]          INT  IDENTITY (1, 1) NOT NULL,
    [CreatedDate] DATE NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);
go
CREATE TABLE [dbo].[TblMove] (
    [Id]          INT  IDENTITY (1, 1) NOT NULL,
    [CreatedDate] DATE NOT NULL,
	[idPlayer]    INT  NOT NULL,
	[idGame]    INT  NOT NULL,
    [From_X]      INT  NOT NULL,
    [From_Y]      INT  NOT NULL,
    [To_X]        INT  NOT NULL,
    [To_Y]        INT  NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
	CONSTRAINT [FK_MoPl_ToPl] FOREIGN KEY ([idPlayer]) REFERENCES [dbo].[TblPlayer] ([Id]),
	CONSTRAINT [FK_GaMo_ToGa] FOREIGN KEY ([idGame]) REFERENCES [dbo].[TblGame] ([Id]) ON DELETE CASCADE,
);



GO
CREATE TABLE [dbo].[TblPlayer] (
    [Id]        INT          IDENTITY (1, 1) NOT NULL,
	[Password] VARCHAR (20) NOT NULL,
    [Name]      VARCHAR (20) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

go

CREATE TABLE [dbo].[TblFamilyPlayer] (
    [Id]       INT IDENTITY (1, 1) NOT NULL,
    [idFamily] INT NOT NULL,
    [idPlayer] INT NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_FamPl_ToFam] FOREIGN KEY ([idFamily]) REFERENCES [dbo].[TblFamily] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FamPl_ToPl] FOREIGN KEY ([idPlayer]) REFERENCES [dbo].[TblPlayer] ([Id]) ON DELETE CASCADE
);

go
CREATE TABLE [dbo].[TblPlayerGame] (
    [Id]       INT IDENTITY (1, 1) NOT NULL,
    [idPlayer] INT NOT NULL,
    [idGame]   INT NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PlGa_ToGa] FOREIGN KEY ([idGame]) REFERENCES [dbo].[TblGame] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PlGa_ToPl] FOREIGN KEY ([idPlayer]) REFERENCES [dbo].[TblPlayer] ([Id])
);

go
CREATE TABLE [dbo].[TblGameMove] (
    [Id]       INT IDENTITY (1, 1) NOT NULL,
    [idMove] INT NOT NULL,
    [idGame]   INT NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_GaMo_ToGa] FOREIGN KEY ([idGame]) REFERENCES [dbo].[TblGame] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GaMo_ToMo] FOREIGN KEY ([idMove]) REFERENCES [dbo].[TblMove] ([Id]) ON DELETE CASCADE
);