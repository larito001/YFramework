//
//  DirichletAdUnityBridge.mm
//  Dirichlet Ad Unity Bridge for iOS
//
//  Created by Dirichlet Unity SDK
//  Copyright © 2025 Dirichlet Inc. All rights reserved.
//

#import "DirichletAdUnityBridge.h"
#import <DirichletAdSDK/DirichletAdSDK.h>
#import <UIKit/UIKit.h>

// Unity callback interface
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

// Constants
static NSString * const kUnityCallbackObject = @"DirichletAdEventReceiver";
static NSString * const kUnityCallbackMethod = @"OnNativeEvent";
static NSString * const kUnityLoadCallbackObject = @"DirichletIOSLoadCallbackReceiver";
static NSString * const kUnityLoadCallbackMethod = @"OnLoadCallback";
static NSString * const kUnityInitCallbackObject = @"DirichletIOSInitCallbackReceiver";
static NSString * const kUnityInitCallbackMethod = @"OnInitCallback";

// Helper to convert C string to NSString
static NSString* CreateNSString(const char* cString) {
    return cString ? [NSString stringWithUTF8String:cString] : @"";
}

// Helper to convert NSString to C string (caller must free)
static char* MakeCString(NSString* nsString) {
    if (nsString == nil) {
        return NULL;
    }
    const char* utf8String = [nsString UTF8String];
    char* cString = (char*)malloc(strlen(utf8String) + 1);
    strcpy(cString, utf8String);
    return cString;
}

// Helper to send event to Unity
static void SendEventToUnity(NSString* handleId, NSString* eventName, NSString* adType, NSDictionary* data) {
    NSMutableDictionary* payload = [NSMutableDictionary dictionary];
    payload[@"handle"] = handleId ?: @"";
    payload[@"eventName"] = eventName ?: @"";
    payload[@"adType"] = adType ?: @"";
    if (data) {
        payload[@"data"] = data;
    }
    
    NSError* error = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    if (jsonData && !error) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage([kUnityCallbackObject UTF8String], [kUnityCallbackMethod UTF8String], [jsonString UTF8String]);
    }
}

// Helper to send load callback to Unity (separate from ad events)
static void SendLoadCallbackToUnity(NSString* handleId, NSString* eventName, NSString* adType, NSDictionary* data) {
    NSMutableDictionary* payload = [NSMutableDictionary dictionary];
    payload[@"handle"] = handleId ?: @"";
    payload[@"eventName"] = eventName ?: @"";
    payload[@"adType"] = adType ?: @"";
    if (data) {
        payload[@"data"] = data;
    }
    
    NSError* error = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    if (jsonData && !error) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage([kUnityLoadCallbackObject UTF8String], [kUnityLoadCallbackMethod UTF8String], [jsonString UTF8String]);
    }
}

// Helper to send init callback to Unity
static void SendInitCallbackToUnity(BOOL success, NSError* error, NSString* extraMessage) {
    NSMutableDictionary* payload = [NSMutableDictionary dictionary];
    payload[@"success"] = @(success);

    NSMutableDictionary* data = [NSMutableDictionary dictionary];
    if (error) {
        data[@"code"] = @(error.code);
        data[@"message"] = error.localizedDescription ?: @"";
        if (error.domain) {
            data[@"domain"] = error.domain;
        }
    } else if (extraMessage.length > 0) {
        data[@"message"] = extraMessage;
    }

    if (data.count > 0) {
        payload[@"data"] = data;
    }

    NSError* jsonError = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&jsonError];
    if (jsonData && !jsonError) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage([kUnityInitCallbackObject UTF8String], [kUnityInitCallbackMethod UTF8String], [jsonString UTF8String]);
    }
}

// Parse JSON extras to dictionary
static NSDictionary* ParseExtras(const char* extrasJson) {
    if (!extrasJson || strlen(extrasJson) == 0) {
        return nil;
    }
    
    NSString* jsonString = CreateNSString(extrasJson);
    NSData* jsonData = [jsonString dataUsingEncoding:NSUTF8StringEncoding];
    if (!jsonData) {
        return nil;
    }
    
    NSError* error = nil;
    NSDictionary* dict = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:&error];
    return error ? nil : dict;
}

#pragma mark - Ad Instance Manager

@interface DirichletAdInstanceManager : NSObject

@property (nonatomic, strong) NSMutableDictionary<NSString*, id>* adInstances;
@property (nonatomic, strong) dispatch_queue_t syncQueue;

+ (instancetype)shared;
- (void)storeAd:(id)ad forHandle:(NSString*)handleId;
- (id)adForHandle:(NSString*)handleId;
- (void)removeAdForHandle:(NSString*)handleId;
- (NSString*)generateHandle;

@end

@implementation DirichletAdInstanceManager

+ (instancetype)shared {
    static DirichletAdInstanceManager* instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[DirichletAdInstanceManager alloc] init];
    });
    return instance;
}

- (instancetype)init {
    if (self = [super init]) {
        _adInstances = [NSMutableDictionary dictionary];
        _syncQueue = dispatch_queue_create("com.dirichlet.unity.admanager", DISPATCH_QUEUE_SERIAL);
    }
    return self;
}

- (void)storeAd:(id)ad forHandle:(NSString*)handleId {
    dispatch_sync(self.syncQueue, ^{
        self.adInstances[handleId] = ad;
    });
}

- (id)adForHandle:(NSString*)handleId {
    __block id ad = nil;
    dispatch_sync(self.syncQueue, ^{
        ad = self.adInstances[handleId];
    });
    return ad;
}

- (void)removeAdForHandle:(NSString*)handleId {
    dispatch_sync(self.syncQueue, ^{
        [self.adInstances removeObjectForKey:handleId];
    });
}

- (NSString*)generateHandle {
    return [[NSUUID UUID] UUIDString];
}

@end

#pragma mark - Ad Delegates

// Rewarded Ad Delegate
@interface DirichletUnityRewardedAdDelegate : NSObject <DRARewardedAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletUnityRewardedAdDelegate

- (void)dirichletAdDidShow:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"show", @"reward_video", nil);
}

- (void)dirichletAdDidClick:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"click", @"reward_video", nil);
}

- (void)dirichletAdDidClose:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"close", @"reward_video", nil);
}

- (void)rewardedAd:(DRARewardedAd *)ad didVerifyReward:(BOOL)verified rewardAmount:(NSInteger)amount rewardName:(NSString *)name {
    NSDictionary* data = @{
        @"rewardVerify": @(verified),
        @"rewardAmount": @(amount),
        @"rewardName": name ?: @"",
        @"code": @(0),
        @"message": @""
    };
    SendEventToUnity(self.handleId, @"reward", @"reward_video", data);
}

@end

// Interstitial Ad Delegate
@interface DirichletUnityInterstitialAdDelegate : NSObject <DRAInterstitialAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletUnityInterstitialAdDelegate

- (void)dirichletAdDidShow:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"show", @"interstitial", nil);
}

- (void)dirichletAdDidClick:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"click", @"interstitial", nil);
}

- (void)dirichletAdDidClose:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"close", @"interstitial", nil);
}

@end

// Banner Ad Delegate
@interface DirichletUnityBannerAdDelegate : NSObject <DRAAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletUnityBannerAdDelegate

- (void)dirichletAdDidShow:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"show", @"banner", nil);
}

- (void)dirichletAdDidClick:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"click", @"banner", nil);
}

- (void)dirichletAdDidClose:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"close", @"banner", nil);
}

@end

// Splash Ad Delegate
@interface DirichletUnitySplashAdDelegate : NSObject <DRASplashAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletUnitySplashAdDelegate

- (void)dirichletAdDidShow:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"show", @"splash", nil);
}

- (void)dirichletAdDidClick:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"click", @"splash", nil);
}

- (void)dirichletAdDidClose:(id<DRAAd>)ad {
    SendEventToUnity(self.handleId, @"close", @"splash", nil);
}

@end

#pragma mark - Bridge Implementation

extern "C" {

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
) {
    NSString* nsMediaId = CreateNSString(mediaId);
    NSString* nsMediaKey = CreateNSString(mediaKey);
    
    NSLog(@"[DirichletUnityBridge] Initialize called with mediaId=%@, channel=%s, isDebug=%d, allowIDFAAccess=%d", 
          nsMediaId, channel ? channel : "NULL", isDebug, allowIDFAAccess);
    
    if (nsMediaId.length == 0 || nsMediaKey.length == 0) {
        NSLog(@"[DirichletUnityBridge] Initialize failed: mediaId and mediaKey are required");
        return false;
    }
    
    // Check if SDK is already initialized
    if ([DirichletAd isInitialized]) {
        NSLog(@"[DirichletUnityBridge] SDK already initialized");
        return true;
    }
    
    DRASDKConfig* config = [DRASDKConfig configWithMediaId:nsMediaId mediaKey:nsMediaKey];
    if (!config) {
        NSLog(@"[DirichletUnityBridge] Failed to create SDK config");
        return false;
    }
    
    config.isDebug = isDebug;
    config.shakeEnabled = shakeEnabled;
    config.allowIDFAAccess = allowIDFAAccess;
    
    if (mediaName && strlen(mediaName) > 0) {
        config.mediaName = CreateNSString(mediaName);
    }
    
    if (tapClientId && strlen(tapClientId) > 0) {
        config.tapClientId = CreateNSString(tapClientId);
    }
    
    if (channel && strlen(channel) > 0) {
        config.gameChannel = CreateNSString(channel);
    }
    
    if (aggregationChannel && strlen(aggregationChannel) > 0) {
        config.aggregationChannel = CreateNSString(aggregationChannel);
    }
    
    if (dataJson && strlen(dataJson) > 0) {
        config.data = CreateNSString(dataJson);
    }
    
    if (aTags && strlen(aTags) > 0) {
        config.aTags = CreateNSString(aTags);
    }
    
    NSLog(@"[DirichletUnityBridge] Starting SDK initialization...");
    NSLog(@"[DirichletUnityBridge] Config details - mediaId:%@, mediaKey:%@, channel:%@, mediaName:%@, tapClientId:%@, allowIDFAAccess:%d",
          config.mediaId, config.mediaKey, config.gameChannel, config.mediaName, config.tapClientId, allowIDFAAccess);
    
    void (^startBlock)(void) = ^{
        [DirichletAd startWithConfig:config completion:^(BOOL success, NSError * _Nullable error) {
            if (error) {
                NSLog(@"[DirichletUnityBridge] SDK init callback - success:%d, error:%@ (code:%ld, domain:%@)",
                      success, error.localizedDescription, (long)error.code, error.domain);
            } else {
                NSLog(@"[DirichletUnityBridge] SDK init callback - success:%d", success);
            }
            SendInitCallbackToUnity(success, error, success ? @"ios_bridge" : nil);
        }];
    };
    
    if ([NSThread isMainThread]) {
        startBlock();
    } else {
        dispatch_async(dispatch_get_main_queue(), startBlock);
    }
    return true;
}

void DirichletAdUnityBridge_RequestPermissionIfNeeded(void) {
    // iOS 14+ ATT permission is handled internally by the SDK
    NSLog(@"[DirichletUnityBridge] RequestPermissionIfNeeded called");
}

const char* DirichletAdUnityBridge_GetSdkVersion(void) {
    static char* versionCString = NULL;
    if (versionCString == NULL) {
        versionCString = MakeCString([DirichletAd sdkVersion]);
    }
    return versionCString;
}

const char* DirichletAdUnityBridge_LoadRewardVideoAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletAdInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Use Builder pattern
    DRARewardedAdLoadRequestBuilder* builder = [DRARewardedAdLoadRequest builder];
    [builder withSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    // Apply extras if provided
    if (extrasDict[@"user_id"]) {
        [builder withRewardUserId:extrasDict[@"user_id"]];
    }
    if (extrasDict[@"reward_name"]) {
        [builder withRewardName:extrasDict[@"reward_name"]];
    }
    if (extrasDict[@"reward_amount"]) {
        [builder withRewardAmount:[extrasDict[@"reward_amount"] integerValue]];
    }
    if (extrasDict[@"extra1"]) {
        [builder withRewardExtras:extrasDict[@"extra1"]];
    }
    if (extrasDict[@"query"]) {
        [builder withQuery:extrasDict[@"query"]];
    }
    if (extrasDict[@"mina_id"]) {
        [builder withMinaId:extrasDict[@"mina_id"]];
    }
    
    DRARewardedAdLoadRequest* request = [builder build];
    
    [DRARewardedAd loadWithRequest:request completion:^(DRARewardedAd * _Nullable ad, NSError * _Nullable error) {
        if (ad) {
            DirichletUnityRewardedAdDelegate* delegate = [[DirichletUnityRewardedAdDelegate alloc] init];
            delegate.handleId = handleId;
            ad.delegate = delegate;
            
            [[DirichletAdInstanceManager shared] storeAd:ad forHandle:handleId];
            [[DirichletAdInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
            
            NSLog(@"[DirichletUnityBridge] RewardVideoAd loaded: %@", handleId);
            SendLoadCallbackToUnity(handleId, @"load_success", @"reward_video", nil);
        } else {
            NSLog(@"[DirichletUnityBridge] RewardVideoAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @([error code]),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"reward_video", errorData);
        }
    }];
    
    return MakeCString(handleId);
}

const char* DirichletAdUnityBridge_LoadInterstitialAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletAdInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Use Builder pattern
    DRAInterstitialAdLoadRequestBuilder* builder = [DRAInterstitialAdLoadRequest builder];
    [builder withSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    if (extrasDict[@"query"]) {
        [builder withQuery:extrasDict[@"query"]];
    }
    if (extrasDict[@"mina_id"]) {
        [builder withMinaId:extrasDict[@"mina_id"]];
    }
    
    DRAInterstitialAdLoadRequest* request = [builder build];
    
    [DRAInterstitialAd loadWithRequest:request completion:^(DRAInterstitialAd * _Nullable ad, NSError * _Nullable error) {
        if (ad) {
            DirichletUnityInterstitialAdDelegate* delegate = [[DirichletUnityInterstitialAdDelegate alloc] init];
            delegate.handleId = handleId;
            ad.delegate = delegate;
            
            [[DirichletAdInstanceManager shared] storeAd:ad forHandle:handleId];
            [[DirichletAdInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
            
            NSLog(@"[DirichletUnityBridge] InterstitialAd loaded: %@", handleId);
            SendLoadCallbackToUnity(handleId, @"load_success", @"interstitial", nil);
        } else {
            NSLog(@"[DirichletUnityBridge] InterstitialAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @([error code]),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"interstitial", errorData);
        }
    }];
    
    return MakeCString(handleId);
}

const char* DirichletAdUnityBridge_LoadBannerAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletAdInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Use Builder pattern to create request
    DRABannerAdLoadRequestBuilder* builder = [DRABannerAdLoadRequest builder];
    [builder withSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    if (extrasDict[@"query"]) {
        [builder withQuery:extrasDict[@"query"]];
    }
    if (extrasDict[@"mina_id"]) {
        [builder withMinaId:extrasDict[@"mina_id"]];
    }
    
    DRABannerAdLoadRequest* request = [builder build];
    
    // Create Banner Ad View instance (similar to BannerAdViewController.m:511)
    DRABannerAdView* bannerView = [[DRABannerAdView alloc] initWithRequest:request];
    
    // Set delegate before loading
    DirichletUnityBannerAdDelegate* delegate = [[DirichletUnityBannerAdDelegate alloc] init];
    delegate.handleId = handleId;
    bannerView.delegate = delegate;
    
    // Store instances immediately (before load completes)
    [[DirichletAdInstanceManager shared] storeAd:bannerView forHandle:handleId];
    [[DirichletAdInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
    
    // Load with completion callback (similar to BannerAdViewController.m:521)
    [bannerView loadWithCompletion:^(NSError * _Nullable error) {
        if (error) {
            NSLog(@"[DirichletUnityBridge] BannerAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @([error code]),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"banner", errorData);
        } else {
            NSLog(@"[DirichletUnityBridge] BannerAd loaded successfully: %@", handleId);
            // Send load success event to Unity load callback receiver (similar to Android's listener.onSuccess())
            SendLoadCallbackToUnity(handleId, @"load_success", @"banner", nil);
        }
    }];
    
    return MakeCString(handleId);
}

const char* DirichletAdUnityBridge_LoadSplashAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletAdInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Use Builder pattern
    DRASplashAdLoadRequestBuilder* builder = [DRASplashAdLoadRequest builder];
    [builder withSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    if (extrasDict[@"query"]) {
        [builder withQuery:extrasDict[@"query"]];
    }
    if (extrasDict[@"mina_id"]) {
        [builder withMinaId:extrasDict[@"mina_id"]];
    }
    
    DRASplashAdLoadRequest* request = [builder build];
    
    [DRASplashAd loadWithRequest:request completion:^(DRASplashAd * _Nullable ad, NSError * _Nullable error) {
        if (ad) {
            DirichletUnitySplashAdDelegate* delegate = [[DirichletUnitySplashAdDelegate alloc] init];
            delegate.handleId = handleId;
            ad.delegate = delegate;
            
            [[DirichletAdInstanceManager shared] storeAd:ad forHandle:handleId];
            [[DirichletAdInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
            
            NSLog(@"[DirichletUnityBridge] SplashAd loaded: %@", handleId);
            SendLoadCallbackToUnity(handleId, @"load_success", @"splash", nil);
        } else {
            NSLog(@"[DirichletUnityBridge] SplashAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @([error code]),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"splash", errorData);
        }
    }];
    
    return MakeCString(handleId);
}

bool DirichletAdUnityBridge_ShowAd(const char* handleId, const char* extras) {
    NSString* nsHandleId = CreateNSString(handleId);
    id ad = [[DirichletAdInstanceManager shared] adForHandle:nsHandleId];
    
    if (!ad) {
        NSLog(@"[DirichletUnityBridge] ShowAd failed: ad not found for handle %@", nsHandleId);
        return false;
    }
    
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        
        if ([ad isKindOfClass:[DRARewardedAd class]]) {
            DRARewardedAd* rewardAd = (DRARewardedAd*)ad;
            [rewardAd showFromViewController:rootViewController];
        } else if ([ad isKindOfClass:[DRAInterstitialAd class]]) {
            DRAInterstitialAd* interstitialAd = (DRAInterstitialAd*)ad;
            [interstitialAd showFromViewController:rootViewController];
        } else if ([ad isKindOfClass:[DRABannerAdView class]]) {
            DRABannerAdView* bannerView = (DRABannerAdView*)ad;
            
            // Parse show options for banner
            NSDictionary* extrasDict = ParseExtras(extras);
            NSInteger baseline = extrasDict[@"banner_baseline"] ? [extrasDict[@"banner_baseline"] integerValue] : 1; // 1 = Bottom
            CGFloat offset = extrasDict[@"banner_offset"] ? [extrasDict[@"banner_offset"] floatValue] : 0;
            
            // Add banner to root view
            UIView* rootView = rootViewController.view;
            [rootView addSubview:bannerView];
            
            // Position banner
            bannerView.translatesAutoresizingMaskIntoConstraints = NO;
            [NSLayoutConstraint activateConstraints:@[
                [bannerView.centerXAnchor constraintEqualToAnchor:rootView.centerXAnchor],
            ]];
            
            if (baseline == 0) { // Top
                [bannerView.topAnchor constraintEqualToAnchor:rootView.safeAreaLayoutGuide.topAnchor constant:offset].active = YES;
            } else { // Bottom
                [bannerView.bottomAnchor constraintEqualToAnchor:rootView.safeAreaLayoutGuide.bottomAnchor constant:-offset].active = YES;
            }
            
        } else if ([ad isKindOfClass:[DRASplashAd class]]) {
            DRASplashAd* splashAd = (DRASplashAd*)ad;
            // Use showFromViewController instead of showInWindow
            [splashAd showFromViewController:rootViewController];
        }
    });
    
    return true;
}

void DirichletAdUnityBridge_DestroyAd(const char* handleId) {
    NSString* nsHandleId = CreateNSString(handleId);
    id ad = [[DirichletAdInstanceManager shared] adForHandle:nsHandleId];
    
    if ([ad isKindOfClass:[DRABannerAdView class]]) {
        dispatch_async(dispatch_get_main_queue(), ^{
            DRABannerAdView* bannerView = (DRABannerAdView*)ad;
            [bannerView removeFromSuperview];
        });
    }
    
    [[DirichletAdInstanceManager shared] removeAdForHandle:nsHandleId];
    [[DirichletAdInstanceManager shared] removeAdForHandle:[nsHandleId stringByAppendingString:@"_delegate"]];
    
    NSLog(@"[DirichletUnityBridge] Ad destroyed: %@", nsHandleId);
}

bool DirichletAdUnityBridge_IsAdValid(const char* handleId) {
    NSString* nsHandleId = CreateNSString(handleId);
    id ad = [[DirichletAdInstanceManager shared] adForHandle:nsHandleId];
    
    if (!ad) {
        NSLog(@"[DirichletUnityBridge] IsAdValid: Ad not found for handle %@", nsHandleId);
        return false;
    }
    
    if ([ad isKindOfClass:[DRARewardedAd class]]) {
        DRARewardedAd* rewardAd = (DRARewardedAd*)ad;
        return [rewardAd isValid];
    } else if ([ad isKindOfClass:[DRAInterstitialAd class]]) {
        DRAInterstitialAd* interstitialAd = (DRAInterstitialAd*)ad;
        return [interstitialAd isValid];
    } else if ([ad isKindOfClass:[DRABannerAdView class]]) {
        // Banner ads don't have isValid, assume valid if loaded
        return true;
    } else if ([ad isKindOfClass:[DRASplashAd class]]) {
        DRASplashAd* splashAd = (DRASplashAd*)ad;
        return [splashAd isValid];
    }
    
    return false;
}

} // extern "C"

