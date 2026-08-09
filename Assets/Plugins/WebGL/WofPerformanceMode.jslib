mergeInto(LibraryManager.library, {
  $WofPerformance_AllocString: function (value) {
    var text = value || "";
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  WofPerformance_GetUserAgent__deps: ["$WofPerformance_AllocString"],
  WofPerformance_GetUserAgent: function () {
    return WofPerformance_AllocString(navigator.userAgent || "");
  },

  WofPerformance_GetPlatform__deps: ["$WofPerformance_AllocString"],
  WofPerformance_GetPlatform: function () {
    return WofPerformance_AllocString(navigator.platform || "");
  },

  WofPerformance_GetMaxTouchPoints: function () {
    return navigator.maxTouchPoints || 0;
  },

  WofPerformance_GetScreenWidth: function () {
    return (window.screen && window.screen.width) || 0;
  },

  WofPerformance_GetScreenHeight: function () {
    return (window.screen && window.screen.height) || 0;
  },

  WofPerformance_GetInnerWidth: function () {
    return window.innerWidth || 0;
  },

  WofPerformance_GetInnerHeight: function () {
    return window.innerHeight || 0;
  },

  WofPerformance_GetQualityPreference__deps: ["$WofPerformance_AllocString"],
  WofPerformance_GetQualityPreference: function () {
    var value = "";
    try { value = window.localStorage.getItem("wizards-quality-performance") || ""; } catch (error) {}
    return WofPerformance_AllocString(value);
  },

  WofPerformance_GetMobilePreference__deps: ["$WofPerformance_AllocString"],
  WofPerformance_GetMobilePreference: function () {
    var value = "";
    try { value = window.localStorage.getItem("wizards-mobile-performance") || ""; } catch (error) {}
    return WofPerformance_AllocString(value);
  }
});
