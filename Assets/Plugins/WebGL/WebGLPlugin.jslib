mergeInto(LibraryManager.library, {
  // <Unity → React> Bootstrap 완료 시 isReady 알림
  OnUnityReady: function (message) {
    var msg = UTF8ToString(message);
    if (console && console.log) {
      console.log('[WebGLPlugin] SendUnityReady:', msg);
    }

    try {
      if (typeof window.dispatchReactUnityEvent === 'function') {
        window.dispatchReactUnityEvent('OnUnityReady', msg);
      }

      if (typeof window.OnUnityReady === 'function') {
        window.OnUnityReady(msg);
      }
    } catch (err) {
      if (console && console.error) {
        console.error('[WebGLPlugin] OnUnityReady error:', err);
      }
    }
  },

  // <Unity → React> Scene 로딩 완료 시 isLoaded 알림
  OnSceneLoaded: function (message) {
    var msg = UTF8ToString(message);
    if (console && console.log) {
      console.log('[WebGLPlugin] SendSceneLoaded:', msg);
    }

    try {
      if (typeof window.dispatchReactUnityEvent === 'function') {
        window.dispatchReactUnityEvent('OnSceneLoaded', msg);
      }

      if (typeof window.OnSceneLoaded === 'function') {
        window.OnSceneLoaded(msg);
      }
    } catch (err) {
      if (console && console.error) {
        console.error('[WebGLPlugin] OnSceneLoaded error:', err);
      }
    }
  },

  // <Unity → React> Scene 로딩 완료 시 isLoaded 알림
  OnSceneUpdated: function (message) {
    var msg = UTF8ToString(message);
    if (console && console.log) {
      console.log('[WebGLPlugin] SendSceneUpdated:', msg);
    }

    try {
      if (typeof window.dispatchReactUnityEvent === 'function') {
        window.dispatchReactUnityEvent('OnSceneUpdated', msg);
      }

      if (typeof window.OnSceneUpdated === 'function') {
        window.OnSceneUpdated(msg);
      }
    } catch (err) {
      if (console && console.error) {
        console.error('[WebGLPlugin] OnSceneUpdated error:', err);
      }
    }
  },
})
