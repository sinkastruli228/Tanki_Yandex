mergeInto(LibraryManager.library, {
  TankiYandexInitialize: function () {
    if (window.tankiYandexBridge) return;

    var bridge = window.tankiYandexBridge = {
      sdk: null,
      readyRequested: false,
      gameplayRequested: false,
      gameplaySent: null
    };

    function applyReady() {
      if (!bridge.sdk || !bridge.readyRequested) return;
      bridge.sdk.features.LoadingAPI && bridge.sdk.features.LoadingAPI.ready();
      bridge.readyRequested = false;
    }

    function applyGameplay() {
      if (!bridge.sdk || bridge.gameplaySent === bridge.gameplayRequested) return;
      var api = bridge.sdk.features.GameplayAPI;
      if (api) {
        if (bridge.gameplayRequested) api.start();
        else api.stop();
      }
      bridge.gameplaySent = bridge.gameplayRequested;
    }

    function initializeSdk() {
      if (typeof YaGames === 'undefined') return;
      YaGames.init().then(function (sdk) {
        bridge.sdk = sdk;
        sdk.on('game_api_pause', function () {
          SendMessage('Tanki Platform Bridge', 'OnYandexPause', '');
        });
        sdk.on('game_api_resume', function () {
          SendMessage('Tanki Platform Bridge', 'OnYandexResume', '');
        });
        applyReady();
        applyGameplay();
      }).catch(function (error) {
        console.warn('Yandex Games SDK initialization failed.', error);
      });
    }

    if (typeof YaGames !== 'undefined') {
      initializeSdk();
      return;
    }

    var script = document.createElement('script');
    script.src = '/sdk.js';
    script.async = true;
    script.onload = initializeSdk;
    script.onerror = function () {
      console.info('Yandex Games SDK is unavailable on this host.');
    };
    document.head.appendChild(script);
  },

  TankiYandexGameReady: function () {
    var bridge = window.tankiYandexBridge;
    if (!bridge) return;
    bridge.readyRequested = true;
    if (bridge.sdk) {
      bridge.sdk.features.LoadingAPI && bridge.sdk.features.LoadingAPI.ready();
      bridge.readyRequested = false;
    }
  },

  TankiYandexSetGameplay: function (active) {
    var bridge = window.tankiYandexBridge;
    if (!bridge) return;
    bridge.gameplayRequested = active !== 0;
    if (!bridge.sdk || bridge.gameplaySent === bridge.gameplayRequested) return;
    var api = bridge.sdk.features.GameplayAPI;
    if (api) {
      if (bridge.gameplayRequested) api.start();
      else api.stop();
    }
    bridge.gameplaySent = bridge.gameplayRequested;
  }
});
