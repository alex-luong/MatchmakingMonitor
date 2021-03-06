﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using MatchmakingMonitor.config;
using MatchmakingMonitor.Models;
using MatchmakingMonitor.Models.Replay;
using MatchmakingMonitor.SocketIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatchmakingMonitor.Services
{
  public class StatsService
  {
    private readonly ApiService _apiService;

    private readonly ILogger _logger;
    private readonly SettingsWrapper _settingsWrapper;
    private readonly SocketIoService _socketIoService;

    private readonly BehaviorSubject<StatsStatus> _statsStatusChangedSubject;
    private readonly BehaviorSubject<List<DisplayPlayerStats>> _statsSubject;

    private Region _currentRegion;
    private Replay _currentReplay;

    public List<DisplayPlayerStats> CurrentStats;

    public StatsService(ILogger logger, SettingsWrapper settingsWrapper, WatcherService watcherService,
      ApiService apiService, SocketIoService socketIoService)
    {
      _logger = logger;
      _apiService = apiService;
      _settingsWrapper = settingsWrapper;
      _socketIoService = socketIoService;

      _statsStatusChangedSubject = new BehaviorSubject<StatsStatus>(StatsStatus.Waiting);
      _statsSubject = new BehaviorSubject<List<DisplayPlayerStats>>(null);

      watcherService.MatchFound.Where(path => path != null)
        .Subscribe(path => { Task.Run(async () => { await StatsFound(path); }); });

      socketIoService.Hub.OnPlayersRequested.SelectMany(_statsSubject).Subscribe(players =>
      {
        try
        {
          socketIoService.Hub.SendColorKeys(players.Select(p => p.GetColorKeys()).ToList());
          socketIoService.Hub.SendPlayers(players.Select(p => p.ToMobile()).ToList());
        }
        catch (Exception e)
        {
          _logger.Error("Error trying to convert stats to mobile version", e);
        }
      });
    }

    public IObservable<StatsStatus> StatsStatusChanged => _statsStatusChangedSubject.AsObservable();
    public IObservable<List<DisplayPlayerStats>> Stats => _statsSubject.Where(s => s != null).AsObservable();

    private async Task StatsFound(string path)
    {
      Replay replay = null;
      var sr = new StreamReader(path);
      var jsonString = await sr.ReadToEndAsync();
      sr.Dispose();
      try
      {
        replay = await Task.Run(() => JsonConvert.DeserializeObject<Replay>(jsonString, new IsoDateTimeConverter()));
      }
      catch (Exception e)
      {
        _logger.Error($"Error while reading replay file ({jsonString})", e);
      }


      if (replay != null)
      {
        var region = _settingsWrapper.CurrentSettings.Region;
        if (_currentReplay == null || region != _currentRegion ||
            _currentReplay != null && replay.DateTime > _currentReplay.DateTime)
        {
          _logger.Info("Valid replay found. Fetching stats");
          _currentReplay = replay;
          _currentRegion = region;
          _statsStatusChangedSubject.OnNext(StatsStatus.Fetching);
          var players = (await _apiService.Players(_currentReplay)).OrderByDescending(p => p.ShipType)
            .ThenByDescending(p => p.ShipTier).ToArray();
          if (players.Count(p => p.AccountId != 0) != 0)
          {
            try
            {
              CurrentStats = await ComputeDisplayPlayer(players);
              _statsStatusChangedSubject.OnNext(StatsStatus.Fetched);
              _socketIoService.Hub.SendColorKeys(CurrentStats.Select(p => p.GetColorKeys()).ToList());
              _socketIoService.Hub.SendPlayers(CurrentStats.Select(p => p.ToMobile()).ToList());
              _statsSubject.OnNext(CurrentStats);
            }
            catch (Exception e)
            {
              _logger.Error("Exception occured when computing player for display", e);
              _statsStatusChangedSubject.OnNext(StatsStatus.Waiting);
            }
          }
          else
          {
            _logger.Info("Less than 6 players with stats found. Something seems to be wrong");
            _statsStatusChangedSubject.OnNext(StatsStatus.WrongRegion);
          }
        }
        else
        {
          _logger.Info("Replay was already shown");
        }
      }
    }

    private async Task<List<DisplayPlayerStats>> ComputeDisplayPlayer(IEnumerable<PlayerShip> players)
    {
      _logger.Info("Computing UI for players");
      return await Task.Run(() => players.Select(p => new DisplayPlayerStats(_settingsWrapper, p)).ToList());
    }
  }


  public enum StatsStatus
  {
    Waiting,
    Fetching,
    Fetched,
    WrongRegion
  }
}