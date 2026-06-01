package com.dirichlet.unity;

import android.app.Activity;
import android.app.Application;
import android.graphics.Color;
import android.text.TextUtils;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewParent;
import android.widget.FrameLayout;

import com.tapsdk.tapad.AdRequest;
import com.tapsdk.tapad.TapAdConfig;
import com.tapsdk.tapad.TapAdCustomController;
import com.tapsdk.tapad.TapAdManager;
import com.tapsdk.tapad.TapAdNative;
import com.tapsdk.tapad.TapAdSdk;
import com.tapsdk.tapad.TapBannerAd;
import com.tapsdk.tapad.TapInterstitialAd;
import com.tapsdk.tapad.TapRewardVideoAd;
import com.tapsdk.tapad.TapSplashAd;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

public final class TapAdUnityBridge {

    private static final String TAG = "TapAdUnityBridge";
    private static final long INIT_TIMEOUT_MS = 5_000L;

    private static final String UNITY_CALLBACK_OBJECT = "DirichletAdEventReceiver";
    private static final String UNITY_CALLBACK_METHOD = "OnNativeEvent";
    private static final String EVENT_SHOW = "show";
    private static final String EVENT_CLOSE = "close";
    private static final String EVENT_CLICK = "click";
    private static final String EVENT_REWARD = "reward";
    private static final String EVENT_SKIP = "skip";

    private static final int TYPE_REWARD = 0;
    private static final int TYPE_INTERSTITIAL = 1;
    private static final int TYPE_BANNER = 2;
    private static final int TYPE_SPLASH = 3;

    private static final Map<String, AdEntry> AD_CACHE = new ConcurrentHashMap<>();
    private static final TapAdCustomController UNITY_CUSTOM_CONTROLLER = new UnityCustomController();

    private TapAdUnityBridge() {
    }

    public interface LoadListener {
        void onSuccess();

        void onError(String code, String message);
    }

    public static boolean initialize(String appId,
                                     String channel,
                                     String subChannel,
                                     boolean enableLog,
                                     String mediaName,
                                     String mediaKey,
                                     String tapClientId,
                                     String dataJson,
                                     boolean shakeEnabled) {
        return initialize(appId, channel, subChannel, enableLog, mediaName, mediaKey, tapClientId, dataJson, shakeEnabled, null, null);
    }

    public static boolean initialize(String appId,
                                     String channel,
                                     String subChannel,
                                     boolean enableLog,
                                     String mediaName,
                                     String mediaKey,
                                     String tapClientId,
                                     String dataJson,
                                     boolean shakeEnabled,
                                     String mediaVersion,
                                     String aggregationChannel) {
        return initializeInternal(appId, channel, subChannel, enableLog, mediaName, mediaKey, tapClientId, dataJson, shakeEnabled, mediaVersion, aggregationChannel);
    }

    private static boolean initializeInternal(String appId,
                                              String channel,
                                              String subChannel,
                                              boolean enableLog,
                                              String mediaName,
                                              String mediaKey,
                                              String tapClientId,
                                              String dataJson,
                                              boolean shakeEnabled,
                                              String mediaVersion,
                                              String aggregationChannel) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.e(TAG, "initialize: Unity activity is null");
            return false;
        }

        final Application application = activity.getApplication();
        final CountDownLatch latch = new CountDownLatch(1);
        final AtomicBoolean success = new AtomicBoolean(false);

        activity.runOnUiThread(() -> {
            try {
                TapAdConfig config = buildConfig(appId, channel, subChannel, enableLog, mediaName, mediaKey, tapClientId, dataJson, shakeEnabled, mediaVersion, aggregationChannel);
                TapAdSdk.init(application, config);
                success.set(true);
            } catch (Throwable t) {
                Log.e(TAG, "initialize error", t);
            } finally {
                latch.countDown();
            }
        });

        try {
            latch.await(INIT_TIMEOUT_MS, TimeUnit.MILLISECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }

        return success.get();
    }

    private static TapAdConfig buildConfig(String appId,
                                           String channel,
                                           String subChannel,
                                           boolean enableLog,
                                           String mediaName,
                                           String mediaKey,
                                           String tapClientId,
                                           String dataJson,
                                           boolean shakeEnabled,
                                           String mediaVersion,
                                           String aggregationChannel) {
        long mediaId = safeParseLong(appId, 0L);
        TapAdConfig.Builder builder = new TapAdConfig.Builder()
                .withMediaId(mediaId)
                .enableDebug(enableLog)
                .shakeEnabled(shakeEnabled)
                .withCustomController(UNITY_CUSTOM_CONTROLLER);

        if (!TextUtils.isEmpty(mediaName)) {
            builder.withMediaName(mediaName);
        }

        if (!TextUtils.isEmpty(mediaKey)) {
            builder.withMediaKey(mediaKey);
        }

        if (!TextUtils.isEmpty(mediaVersion)) {
            builder.withMediaVersion(mediaVersion);
        }

        if (!TextUtils.isEmpty(channel)) {
            builder.withGameChannel(channel);
        }

        if (!TextUtils.isEmpty(aggregationChannel)) {
            builder.withAggregationChannel(aggregationChannel);
        }

        if (!TextUtils.isEmpty(tapClientId)) {
            builder.withTapClientId(tapClientId);
        }

        String payload = TextUtils.isEmpty(dataJson) ? buildSubChannelPayload(subChannel) : dataJson;
        if (!TextUtils.isEmpty(payload)) {
            builder.withData(payload);
        }

        return builder.build();
    }

    private static String buildSubChannelPayload(String subChannel) {
        if (TextUtils.isEmpty(subChannel)) {
            return null;
        }

        try {
            JSONObject json = new JSONObject();
            json.put("sub_channel", subChannel);
            return json.toString();
        } catch (JSONException e) {
            Log.w(TAG, "buildSubChannelPayload error", e);
            return null;
        }
    }

    private static long safeParseLong(String value, long fallback) {
        if (TextUtils.isEmpty(value)) {
            return fallback;
        }

        try {
            return Long.parseLong(value.trim());
        } catch (NumberFormatException ignore) {
            return fallback;
        }
    }

    public static void requestPermissionIfNeeded() {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.w(TAG, "requestPermissionIfNeeded: activity is null");
            return;
        }

        activity.runOnUiThread(() -> TapAdManager.get().requestPermissionIfNecessary(activity));
    }

    public static String getSdkVersion() {
        try {
            Class<?> buildConfig = Class.forName("com.tapsdk.tapad.BuildConfig");
            Object value = buildConfig.getField("SDK_VERSION_NAME").get(null);
            if (value != null) {
                return String.valueOf(value);
            }
        } catch (Throwable t) {
            Log.w(TAG, "getSdkVersion fallback", t);
        }
        return "android-unknown";
    }

    public static String loadRewardVideoAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_REWARD, extras, listener);
    }

    public static String loadInterstitialAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_INTERSTITIAL, extras, listener);
    }

    public static String loadBannerAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_BANNER, extras, listener);
    }

    public static String loadSplashAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_SPLASH, extras, listener);
    }

    private static String loadAdInternal(int adType, JSONObject extras, LoadListener listener) {
        if (extras == null) {
            if (listener != null) {
                listener.onError("invalid_request", "Request extras cannot be null");
            }
            return null;
        }

        long spaceId = extras.optLong("space_id", 0L);
        if (spaceId <= 0L) {
            if (listener != null) {
                listener.onError("invalid_space_id", "space_id must be greater than zero");
            }
            return null;
        }

        String handle = UUID.randomUUID().toString();
        AdEntry entry = new AdEntry(handle, adType);
        entry.spaceId = spaceId;
        AD_CACHE.put(handle, entry);

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            AD_CACHE.remove(handle);
            if (listener != null) {
                listener.onError("activity_null", "Unity activity is null");
            }
            return null;
        }

        activity.runOnUiThread(() -> {
            try {
                TapAdNative adNative = TapAdManager.get().createAdNative(activity);
                AdRequest request = buildRequest(entry, extras);

                switch (adType) {
                    case TYPE_REWARD:
                        adNative.loadRewardVideoAd(request, new TapAdNative.RewardVideoAdListener() {
                            @Override
                            public void onRewardVideoAdLoad(TapRewardVideoAd rewardVideoAd) {
                                entry.rewardAd = rewardVideoAd;
                                attachRewardListener(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onRewardVideoCached(TapRewardVideoAd rewardVideoAd) {
                                // Intentionally ignored to match Unity bridge semantics (load event already emitted).
                            }

                            @Override
                            public void onError(int code, String message) {
                                AD_CACHE.remove(handle);
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    case TYPE_INTERSTITIAL:
                        adNative.loadInterstitialAd(request, new TapAdNative.InterstitialAdListener() {
                            @Override
                            public void onInterstitialAdLoad(TapInterstitialAd interstitialAd) {
                                entry.interstitialAd = interstitialAd;
                                attachInterstitialListener(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onError(int code, String message) {
                                AD_CACHE.remove(handle);
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    case TYPE_BANNER:
                        adNative.loadBannerAd(request, new TapAdNative.BannerAdListener() {
                            @Override
                            public void onBannerAdLoad(TapBannerAd bannerAd) {
                                entry.bannerAd = bannerAd;
                                attachBannerListener(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onError(int code, String message) {
                                AD_CACHE.remove(handle);
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    case TYPE_SPLASH:
                        adNative.loadSplashAd(request, new TapAdNative.SplashAdListener() {
                            @Override
                            public void onSplashAdLoad(TapSplashAd splashAd) {
                                entry.splashAd = splashAd;
                                attachSplashListener(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onError(int code, String message) {
                                AD_CACHE.remove(handle);
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    default:
                        AD_CACHE.remove(handle);
                        if (listener != null) {
                            listener.onError("unsupported_type", "Unsupported ad type: " + adType);
                        }
                        break;
                }
            } catch (IllegalArgumentException e) {
                AD_CACHE.remove(handle);
                if (listener != null) {
                    listener.onError("invalid_request", e.getMessage());
                }
            } catch (Throwable t) {
                AD_CACHE.remove(handle);
                Log.e(TAG, "loadAdInternal exception", t);
                if (listener != null) {
                    listener.onError("exception", t.getMessage());
                }
            }
        });

        return handle;
    }

    private static AdRequest buildRequest(AdEntry entry, JSONObject extras) {
        long spaceId = extras.optLong("space_id", 0L);
        if (spaceId <= 0) {
            throw new IllegalArgumentException("space_id must be provided and greater than zero");
        }

        AdRequest.Builder builder = new AdRequest.Builder()
                .withSpaceId(spaceId);

        String extra1 = extras.optString("extra1", null);
        if (!TextUtils.isEmpty(extra1)) {
            builder.withExtra1(extra1);
        }

        String userId = extras.optString("user_id", null);
        if (!TextUtils.isEmpty(userId)) {
            builder.withUserId(userId);
        }

        String rewardName = extras.optString("reward_name", null);
        if (!TextUtils.isEmpty(rewardName)) {
            builder.withRewardName(rewardName);
        }

        if (extras.has("reward_amount")) {
            builder.withRewardAmount(extras.optInt("reward_amount"));
        }

        String query = extras.optString("query", null);
        if (!TextUtils.isEmpty(query)) {
            builder.withQuery(query);
        }

        int expressWidth = resolveExpressValue(extras, "express_width", "express_view_width");
        int expressHeight = resolveExpressValue(extras, "express_height", "express_view_height");
        if (expressWidth != Integer.MIN_VALUE || expressHeight != Integer.MIN_VALUE) {
            int width = expressWidth != Integer.MIN_VALUE ? expressWidth : -1;
            int height = expressHeight != Integer.MIN_VALUE ? expressHeight : -1;
            builder.withExpressViewAcceptedSize(width, height);

            if (entry.type == TYPE_SPLASH) {
                entry.splashWidth = width;
                entry.splashHeight = height;
            }
        }

        if (extras.has("mina_id")) {
            builder.withMinaId(extras.optLong("mina_id"));
        }

        return builder.build();
    }

    private static int resolveExpressValue(JSONObject extras, String primary, String alternate) {
        if (extras.has(primary)) {
            return extras.optInt(primary, -1);
        }

        if (!TextUtils.isEmpty(alternate) && extras.has(alternate)) {
            return extras.optInt(alternate, -1);
        }

        return Integer.MIN_VALUE;
    }

    public static boolean showAd(String handle, JSONObject options) {
        AdEntry entry = AD_CACHE.get(handle);
        if (entry == null) {
            Log.w(TAG, "showAd: handle not found " + handle);
            return false;
        }

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.w(TAG, "showAd: activity null");
            return false;
        }

        CountDownLatch latch = new CountDownLatch(1);
        AtomicBoolean result = new AtomicBoolean(false);

        activity.runOnUiThread(() -> {
            try {
                switch (entry.type) {
                    case TYPE_REWARD:
                        if (entry.rewardAd != null) {
                            entry.rewardAd.showRewardVideoAd(activity);
                            result.set(true);
                        }
                        break;
                    case TYPE_INTERSTITIAL:
                        if (entry.interstitialAd != null) {
                            entry.interstitialAd.show(activity);
                            result.set(true);
                        }
                        break;
                    case TYPE_BANNER:
                        if (entry.bannerAd != null) {
                            int baseline = options != null ? options.optInt("banner_baseline", 1) : 1;
                            int offset = options != null ? options.optInt("banner_offset", 0) : 0;
                            entry.bannerAd.show(activity, baseline, offset);
                            result.set(true);
                        }
                        break;
                    case TYPE_SPLASH:
                        if (entry.splashAd != null) {
                            FrameLayout container = attachSplashContainer(activity, entry);
                            if (container != null) {
                                entry.splashAd.show(container);
                                result.set(true);
                            }
                        }
                        break;
                    default:
                        Log.w(TAG, "showAd: unsupported type " + entry.type);
                        break;
                }
            } catch (Throwable t) {
                Log.e(TAG, "showAd exception", t);
                detachSplashContainer(entry);
            } finally {
                latch.countDown();
            }
        });

        try {
            latch.await(1, TimeUnit.SECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }

        return result.get();
    }

    public static void destroyAd(String handle) {
        AdEntry entry = AD_CACHE.remove(handle);
        if (entry == null) {
            return;
        }

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            entry.destroy();
            return;
        }

        activity.runOnUiThread(entry::destroy);
    }

    /**
     * Checks if an ad is still valid and can be shown.
     * This should be called before show() to ensure the ad hasn't expired.
     * 
     * @param handle The handle ID returned from the load method
     * @return true if the ad is valid and can be shown, false otherwise
     */
    public static boolean isAdValid(String handle) {
        AdEntry entry = AD_CACHE.get(handle);
        if (entry == null) {
            Log.w(TAG, "isAdValid: handle not found " + handle);
            return false;
        }

        try {
            switch (entry.type) {
                case TYPE_REWARD:
                    return entry.rewardAd != null && entry.rewardAd.isValid();
                case TYPE_INTERSTITIAL:
                    return entry.interstitialAd != null && entry.interstitialAd.isValid();
                case TYPE_BANNER:
                    return entry.bannerAd != null;
                case TYPE_SPLASH:
                    return entry.splashAd != null && entry.splashAd.isValid();
                default:
                    return false;
            }
        } catch (Throwable t) {
            Log.w(TAG, "isAdValid exception", t);
            return false;
        }
    }

    private static void attachRewardListener(final AdEntry entry) {
        if (entry == null || entry.rewardAd == null) {
            return;
        }

        entry.rewardAd.setRewardAdInteractionListener(new TapRewardVideoAd.RewardAdInteractionListener() {
            @Override
            public void onAdShow(TapRewardVideoAd ad) {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdClose(TapRewardVideoAd ad) {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
            }

            @Override
            public void onVideoComplete(TapRewardVideoAd ad) {
                // no-op
            }

            @Override
            public void onVideoError(TapRewardVideoAd ad) {
                Log.w(TAG, "Reward video error for handle " + entry.handle);
            }

            @Override
            public void onRewardVerify(TapRewardVideoAd ad, boolean rewardVerify, int rewardAmount, String rewardName, int code, String msg) {
                JSONObject data = new JSONObject();
                try {
                    data.put("rewardVerify", rewardVerify);
                    data.put("rewardAmount", rewardAmount);
                    data.put("rewardName", rewardName == null ? "" : rewardName);
                    data.put("code", code);
                    data.put("message", msg == null ? "" : msg);
                } catch (JSONException e) {
                    Log.w(TAG, "Failed to build reward payload", e);
                }
                emitEvent(entry.handle, EVENT_REWARD, resolveAdType(entry.type), data);
            }

            @Override
            public void onSkippedVideo(TapRewardVideoAd ad) {
                emitEvent(entry.handle, EVENT_SKIP, resolveAdType(entry.type));
            }

            @Override
            public void onAdClick(TapRewardVideoAd ad) {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }

            @Override
            public void onAdValidShow(TapRewardVideoAd ad) {
                // no-op
            }
        });
    }

    private static void attachInterstitialListener(final AdEntry entry) {
        if (entry == null || entry.interstitialAd == null) {
            return;
        }

        entry.interstitialAd.setInteractionListener(new TapInterstitialAd.InterstitialAdInteractionListener() {
            @Override
            public void onAdShow() {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdClose() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
            }

            @Override
            public void onAdError() {
                Log.w(TAG, "Interstitial error for handle " + entry.handle);
            }

            @Override
            public void onAdValidShow() {
                // no-op
            }

            @Override
            public void onAdClick() {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }
        });
    }

    private static void attachBannerListener(final AdEntry entry) {
        if (entry == null || entry.bannerAd == null) {
            return;
        }

        entry.bannerAd.setBannerInteractionListener(new TapBannerAd.BannerInteractionListener() {
            @Override
            public void onAdShow() {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdClose() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
            }

            @Override
            public void onAdClick() {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }

            @Override
            public void onAdValidShow() {
                // no-op
            }
        });
    }

    private static void attachSplashListener(final AdEntry entry) {
        if (entry == null || entry.splashAd == null) {
            return;
        }

        entry.splashAd.setSplashInteractionListener(new TapSplashAd.AdInteractionListener() {
            @Override
            public void onAdSkip() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
                detachSplashContainer(entry);
            }

            @Override
            public void onAdTimeOver() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
                detachSplashContainer(entry);
            }

            @Override
            public void onAdClick() {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }

            @Override
            public void onAdShow() {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdValidShow() {
                // no-op
            }
        });
    }

    private static FrameLayout attachSplashContainer(Activity activity, AdEntry entry) {
        if (activity == null || entry == null) {
            return null;
        }

        ViewGroup root = activity.findViewById(android.R.id.content);
        if (root == null) {
            Log.w(TAG, "attachSplashContainer: root content view is null");
            return null;
        }

        detachSplashContainer(entry);

        FrameLayout overlay = new FrameLayout(activity);
        FrameLayout.LayoutParams overlayParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT);
        overlay.setLayoutParams(overlayParams);
        overlay.setClickable(true);
        overlay.setFocusable(true);
        overlay.setBackgroundColor(Color.BLACK);

        int width = entry.splashWidth > 0 ? entry.splashWidth : FrameLayout.LayoutParams.MATCH_PARENT;
        int height = entry.splashHeight > 0 ? entry.splashHeight : FrameLayout.LayoutParams.MATCH_PARENT;
        FrameLayout.LayoutParams slotParams = new FrameLayout.LayoutParams(width, height);
        slotParams.gravity = Gravity.CENTER;

        FrameLayout slot = new FrameLayout(activity);
        slot.setLayoutParams(slotParams);

        overlay.addView(slot);
        root.addView(overlay);
        entry.splashContainer = overlay;
        return slot;
    }

    private static void detachSplashContainer(AdEntry entry) {
        if (entry == null) {
            return;
        }

        FrameLayout container = entry.splashContainer;
        if (container == null) {
            return;
        }

        entry.splashContainer = null;
        ViewParent parent = container.getParent();
        if (parent instanceof ViewGroup) {
            ((ViewGroup) parent).removeView(container);
        }
    }

    private static void emitEvent(String handle, String eventName, String adType) {
        emitEvent(handle, eventName, adType, null);
    }

    private static void emitEvent(String handle, String eventName, String adType, JSONObject data) {
        if (TextUtils.isEmpty(handle) || TextUtils.isEmpty(eventName)) {
            return;
        }

        try {
            JSONObject message = new JSONObject();
            message.put("handle", handle);
            message.put("eventName", eventName);
            if (!TextUtils.isEmpty(adType)) {
                message.put("adType", adType);
            }
            if (data != null && data.length() > 0) {
                message.put("data", data);
            }
            UnityPlayer.UnitySendMessage(UNITY_CALLBACK_OBJECT, UNITY_CALLBACK_METHOD, message.toString());
        } catch (Exception ex) {
            Log.w(TAG, "Failed to emit unity event", ex);
        }
    }

    private static String resolveAdType(int type) {
        switch (type) {
            case TYPE_REWARD:
                return "reward";
            case TYPE_INTERSTITIAL:
                return "interstitial";
            case TYPE_BANNER:
                return "banner";
            case TYPE_SPLASH:
                return "splash";
            default:
                return "unknown";
        }
    }

    private static final class AdEntry {
        final String handle;
        final int type;

        long spaceId;

        TapRewardVideoAd rewardAd;
        TapInterstitialAd interstitialAd;
        TapBannerAd bannerAd;
        TapSplashAd splashAd;

        FrameLayout splashContainer;
        int splashWidth;
        int splashHeight;

        AdEntry(String handle, int type) {
            this.handle = handle;
            this.type = type;
        }

        void destroy() {
            try {
                switch (type) {
                    case TYPE_REWARD:
                        if (rewardAd != null) {
                            rewardAd.setRewardAdInteractionListener(null);
                            rewardAd.dispose();
                            rewardAd = null;
                        }
                        break;
                    case TYPE_INTERSTITIAL:
                        if (interstitialAd != null) {
                            interstitialAd.setInteractionListener(null);
                            interstitialAd.dispose();
                            interstitialAd = null;
                        }
                        break;
                    case TYPE_BANNER:
                        if (bannerAd != null) {
                            bannerAd.setBannerInteractionListener(null);
                            bannerAd.dispose();
                            bannerAd = null;
                        }
                        break;
                    case TYPE_SPLASH:
                        if (splashAd != null) {
                            splashAd.setSplashInteractionListener(null);
                            splashAd.dispose();
                            splashAd = null;
                        }
                        detachSplashContainer(this);
                        break;
                }
            } catch (Throwable t) {
                Log.w(TAG, "destroyAd exception", t);
            }
        }
    }

    private static final class UnityCustomController extends TapAdCustomController {
    }
}
