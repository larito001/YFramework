//
//  DirichletAdUnityBridge.h
//  Dirichlet Ad Unity Bridge for iOS
//
//  Created by Dirichlet Unity SDK
//  Copyright © 2025 Dirichlet Inc. All rights reserved.
//

#import <Foundation/Foundation.h>

#ifdef __cplusplus
extern "C" {
#endif

/// Initialize the Dirichlet SDK
/// @param mediaId Media ID string
/// @param channel Game channel string
/// @param isDebug Enable debug logging
/// @param mediaName Media name string
/// @param mediaKey Media key string
/// @param tapClientId TapTap client ID (optional)
/// @param dataJson Custom data JSON string (optional)
/// @param shakeEnabled Enable shake interaction
/// @param mediaVersion Media version string (optional)
/// @param aggregationChannel Aggregation channel string (optional)
/// @param allowIDFAAccess Allow IDFA access (iOS only, controls ATT behavior)
/// @param aTags External aTags JSON string (optional, cross-platform)
/// @return YES if initialization started successfully, NO otherwise
bool DirichletAdUnityBridge_Initialize(
    const char* mediaId,
    const char* channel,
    bool isDebug,
    const char* mediaName,
    const char* mediaKey,
    const char* tapClientId,
    const char* dataJson,
    bool shakeEnabled,
    const char* mediaVersion,
    const char* aggregationChannel,
    bool allowIDFAAccess,
    const char* aTags
);

/// Request permissions if necessary
void DirichletAdUnityBridge_RequestPermissionIfNeeded(void);

/// Get SDK version
/// @return SDK version string (caller should NOT free this pointer)
const char* DirichletAdUnityBridge_GetSdkVersion(void);

/// Load reward video ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance (caller should copy/free)
const char* DirichletAdUnityBridge_LoadRewardVideoAd(long long spaceId, const char* extras);

/// Load interstitial ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance
const char* DirichletAdUnityBridge_LoadInterstitialAd(long long spaceId, const char* extras);

/// Load banner ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance
const char* DirichletAdUnityBridge_LoadBannerAd(long long spaceId, const char* extras);

/// Load splash ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance
const char* DirichletAdUnityBridge_LoadSplashAd(long long spaceId, const char* extras);

/// Show ad by handle
/// @param handleId Handle ID returned from load methods
/// @param extras JSON string with show options (e.g., banner alignment)
/// @return YES if show started successfully, NO otherwise
bool DirichletAdUnityBridge_ShowAd(const char* handleId, const char* extras);

/// Destroy ad by handle
/// @param handleId Handle ID returned from load methods
void DirichletAdUnityBridge_DestroyAd(const char* handleId);

#ifdef __cplusplus
}
#endif

