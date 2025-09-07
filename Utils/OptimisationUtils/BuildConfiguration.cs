using UnityEngine;

/// <summary>
/// Build configuration script that ensures proper settings for different platforms
/// This script should be called during build process or at runtime
/// </summary>
public class BuildConfiguration : MonoBehaviour
{
    [Header("Platform Settings")]
    public bool configureForMobile = true;
    public bool configureForDesktop = true;
    
    [Header("Build Settings")]
    public bool optimizeForRelease = true;
    public bool enableDebugLogs = false;
    
    void Awake()
    {
        ConfigureForCurrentPlatform();
    }
    
    public void ConfigureForCurrentPlatform()
    {
        Debug.Log($"Configuring build for platform: {Application.platform}");
        
        // Configure based on current platform
        if (IsMobilePlatform())
        {
            ConfigureForMobile();
        }
        else
        {
            ConfigureForDesktop();
        }
        
        // Apply release optimizations if needed
        if (optimizeForRelease)
        {
            ApplyReleaseOptimizations();
        }
        
        // Configure debug settings
        ConfigureDebugSettings();
    }
    
    void ConfigureForMobile()
    {
        Debug.Log("Configuring for mobile platform...");
        
        // Frame rate settings
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        
        // Quality settings for mobile
        QualitySettings.SetQualityLevel(1, true); // Good quality
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.masterTextureLimit = 1;
        
        // Audio settings
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        audioConfig.dspBufferSize = 256;
        AudioSettings.Reset(audioConfig);
        
        // Physics settings
        Physics2D.autoSimulation = true;
        Physics2D.autoSyncTransforms = false;
        
        // Rendering settings
        QualitySettings.billboardsFaceCameraPosition = true;
        QualitySettings.particleRaycastBudget = 64;
        
        Debug.Log("Mobile configuration complete");
    }
    
    void ConfigureForDesktop()
    {
        Debug.Log("Configuring for desktop platform...");
        
        // Frame rate settings
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 1;
        
        // Quality settings for desktop
        QualitySettings.SetQualityLevel(2, true); // Beautiful quality
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.masterTextureLimit = 0;
        
        // Audio settings
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        audioConfig.dspBufferSize = 512;
        AudioSettings.Reset(audioConfig);
        
        // Physics settings
        Physics2D.autoSimulation = true;
        Physics2D.autoSyncTransforms = true;
        
        // Rendering settings
        QualitySettings.billboardsFaceCameraPosition = false;
        QualitySettings.particleRaycastBudget = 256;
        
        Debug.Log("Desktop configuration complete");
    }
    
    void ApplyReleaseOptimizations()
    {
        Debug.Log("Applying release optimizations...");
        
        // Disable debug features
        Debug.unityLogger.logEnabled = enableDebugLogs;
        
        // Optimize for release
        QualitySettings.lodBias = 1.0f;
        QualitySettings.maximumLODLevel = 0;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.shadowDistance = 50f;
        
        // Memory optimizations
        QualitySettings.billboardsFaceCameraPosition = true;
        
        Debug.Log("Release optimizations applied");
    }
    
    void ConfigureDebugSettings()
    {
        // Configure debug logging
        if (enableDebugLogs)
        {
            Debug.Log("Debug logging enabled");
        }
        else
        {
            Debug.Log("Debug logging disabled for performance");
        }
    }
    
    bool IsMobilePlatform()
    {
        return Application.platform == RuntimePlatform.Android || 
               Application.platform == RuntimePlatform.IPhonePlayer;
    }
    
    // Public methods for runtime configuration
    public void SwitchToMobileConfiguration()
    {
        ConfigureForMobile();
    }
    
    public void SwitchToDesktopConfiguration()
    {
        ConfigureForDesktop();
    }
    
    public void ToggleDebugLogs()
    {
        enableDebugLogs = !enableDebugLogs;
        Debug.unityLogger.logEnabled = enableDebugLogs;
        Debug.Log($"Debug logs: {(enableDebugLogs ? "Enabled" : "Disabled")}");
    }
    
    public void ApplyPerformanceOptimizations()
    {
        // Force garbage collection
        System.GC.Collect();
        
        // Optimize quality settings
        QualitySettings.SetQualityLevel(1, true);
        
        // Reduce frame rate for better performance
        Application.targetFrameRate = 60;
        
        Debug.Log("Performance optimizations applied");
    }
}
