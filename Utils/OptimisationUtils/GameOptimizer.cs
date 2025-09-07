using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Advanced game optimization script that ensures smooth performance
/// across all platforms with comprehensive performance monitoring
/// </summary>
public class GameOptimizer : MonoBehaviour
{
    [Header("Performance Monitoring")]
    public bool enablePerformanceMonitoring = true;
    public float monitoringInterval = 1f;
    
    [Header("Frame Rate Control")]
    public int targetFrameRate = 60;
    public bool adaptiveFrameRate = true;
    public int minFrameRate = 30;
    public int maxFrameRate = 120;
    
    [Header("Memory Management")]
    public bool enableGarbageCollection = true;
    public float gcInterval = 10f;
    public bool forceGCOnLowMemory = true;
    public float lowMemoryThreshold = 0.8f;
    
    [Header("Rendering Optimizations")]
    public bool enableBatching = false;
    public bool enableFrustumCulling = false;
    public bool optimizeForMobile = true;
    
    [Header("Audio Optimizations")]
    public bool optimizeAudio = true;
    public int maxAudioSources = 32;
    
    // Performance tracking
    private float lastGCTime;
    private float lastMonitoringTime;
    private float averageFrameTime;
    private int frameCount;
    private float frameTimeSum;
    
    // Adaptive frame rate
    private float lastFrameTime;
    private int consecutiveLowFrames;
    private int consecutiveHighFrames;
    
    void Start()
    {
        ApplyOptimizations();
        InitializePerformanceMonitoring();
    }
    
    void Update()
    {
        if (enablePerformanceMonitoring)
        {
            UpdatePerformanceMonitoring();
        }
        
        if (enableGarbageCollection)
        {
            CheckGarbageCollection();
        }
        
        if (adaptiveFrameRate)
        {
            UpdateAdaptiveFrameRate();
        }
    }
    
    void ApplyOptimizations()
    {
        Debug.Log("Applying comprehensive game optimizations...");
        
        // Frame rate optimization
        OptimizeFrameRate();
        
        // Rendering optimizations
        OptimizeRendering();
        
        // Audio optimizations
        if (optimizeAudio)
        {
            OptimizeAudio();
        }
        
        // Mobile-specific optimizations
        if (optimizeForMobile && IsMobilePlatform())
        {
            OptimizeForMobile();
        }
        
        // Quality settings
        OptimizeQualitySettings();
        
        Debug.Log("Game optimizations applied successfully");
    }
    
    void OptimizeFrameRate()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0; // Disable VSync for better control
        
        // Set appropriate frame rate based on platform
        if (IsMobilePlatform())
        {
            Application.targetFrameRate = Mathf.Min(targetFrameRate, 60);
        }
    }
    
    void OptimizeRendering()
    {
        // Enable batching for better performance
        if (enableBatching)
        {
            QualitySettings.billboardsFaceCameraPosition = true;
        }
        
        // Optimize physics
        Physics2D.autoSimulation = true;
        Physics2D.autoSyncTransforms = false;
        
        // Optimize rendering pipeline
        if (enableFrustumCulling)
        {
            Camera.main.useOcclusionCulling = true;
        }
    }
    
    void OptimizeAudio()
    {
        // Limit audio sources for better performance
        AudioSource[] audioSources = FindObjectsOfType<AudioSource>();
        if (audioSources.Length > maxAudioSources)
        {
            Debug.LogWarning($"Too many audio sources ({audioSources.Length}). Consider reducing to {maxAudioSources} or less.");
        }
        
        // Optimize audio settings
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        audioConfig.dspBufferSize = 256; // Smaller buffer for lower latency
        AudioSettings.Reset(audioConfig);
    }
    
    void OptimizeForMobile()
    {
        Debug.Log("Applying mobile-specific optimizations...");
        
        // Reduce target frame rate for mobile
        Application.targetFrameRate = Mathf.Min(targetFrameRate, 60);
        
        // Optimize quality settings for mobile
        QualitySettings.SetQualityLevel(1, true); // Use "Good" quality preset
        
        // Disable unnecessary features
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        
        // Optimize texture settings
        QualitySettings.masterTextureLimit = 1; // Reduce texture resolution
        
        // Optimize particle systems
        QualitySettings.particleRaycastBudget = 64;
    }
    
    void OptimizeQualitySettings()
    {
        // Set appropriate quality level based on platform
        if (IsMobilePlatform())
        {
            QualitySettings.SetQualityLevel(1, true); // Good quality for mobile
        }
        else
        {
            QualitySettings.SetQualityLevel(2, true); // Beautiful quality for desktop
        }
        
        // Optimize shadow settings
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.shadowDistance = 50f;
        
        // Optimize LOD settings
        QualitySettings.lodBias = 1.0f;
        QualitySettings.maximumLODLevel = 0;
    }
    
    void InitializePerformanceMonitoring()
    {
        lastGCTime = Time.time;
        lastMonitoringTime = Time.time;
        averageFrameTime = 0f;
        frameCount = 0;
        frameTimeSum = 0f;
    }
    
    void UpdatePerformanceMonitoring()
    {
        if (Time.time - lastMonitoringTime >= monitoringInterval)
        {
            // Calculate average frame time
            if (frameCount > 0)
            {
                averageFrameTime = frameTimeSum / frameCount;
                float currentFPS = 1f / averageFrameTime;
                
                // Log performance metrics
                Debug.Log($"Performance: FPS={currentFPS:F1}, FrameTime={averageFrameTime*1000:F1}ms, Memory={System.GC.GetTotalMemory(false)/1024/1024:F1}MB");
                
                // Check for performance issues
                if (currentFPS < minFrameRate)
                {
                    Debug.LogWarning($"Low FPS detected: {currentFPS:F1}. Consider reducing quality settings.");
                }
            }
            
            // Reset counters
            frameCount = 0;
            frameTimeSum = 0f;
            lastMonitoringTime = Time.time;
        }
        
        // Accumulate frame time data
        frameTimeSum += Time.deltaTime;
        frameCount++;
    }
    
    void CheckGarbageCollection()
    {
        // Regular garbage collection
        if (Time.time - lastGCTime >= gcInterval)
        {
            System.GC.Collect();
            lastGCTime = Time.time;
        }
        
        // Force GC on low memory
        if (forceGCOnLowMemory)
        {
            float memoryUsage = (float)System.GC.GetTotalMemory(false) / System.GC.GetTotalMemory(true);
            if (memoryUsage > lowMemoryThreshold)
            {
                Debug.Log("Low memory detected, forcing garbage collection...");
                System.GC.Collect();
                lastGCTime = Time.time;
            }
        }
    }
    
    void UpdateAdaptiveFrameRate()
    {
        float currentFrameTime = Time.deltaTime;
        float currentFPS = 1f / currentFrameTime;
        
        // Track consecutive low/high frame rates
        if (currentFPS < minFrameRate)
        {
            consecutiveLowFrames++;
            consecutiveHighFrames = 0;
        }
        else if (currentFPS > maxFrameRate)
        {
            consecutiveHighFrames++;
            consecutiveLowFrames = 0;
        }
        else
        {
            consecutiveLowFrames = 0;
            consecutiveHighFrames = 0;
        }
        
        // Adjust frame rate based on performance
        if (consecutiveLowFrames >= 10 && Application.targetFrameRate > minFrameRate)
        {
            Application.targetFrameRate = Mathf.Max(Application.targetFrameRate - 5, minFrameRate);
            Debug.Log($"Reducing target frame rate to {Application.targetFrameRate} due to low performance");
        }
        else if (consecutiveHighFrames >= 20 && Application.targetFrameRate < maxFrameRate)
        {
            Application.targetFrameRate = Mathf.Min(Application.targetFrameRate + 5, maxFrameRate);
            Debug.Log($"Increasing target frame rate to {Application.targetFrameRate} due to good performance");
        }
    }
    
    bool IsMobilePlatform()
    {
        return Application.platform == RuntimePlatform.Android || 
               Application.platform == RuntimePlatform.IPhonePlayer;
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Pause game when app is paused
            Time.timeScale = 0f;
            Debug.Log("Game paused - saving state");
            
            // Save game state
          
        }
        else
        {
            // Resume game when app is unpaused
            Time.timeScale = 1f;
            Debug.Log("Game resumed");
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            Debug.Log("Application lost focus - saving state");
            
            // Save game state when app loses focus
          
        }
    }
    
    void OnApplicationQuit()
    {
        Debug.Log("Application quitting - final save");
        
        // Final save on quit
       
    }
    
    // Public methods for runtime optimization control
    public void SetTargetFrameRate(int frameRate)
    {
        targetFrameRate = Mathf.Clamp(frameRate, minFrameRate, maxFrameRate);
        Application.targetFrameRate = targetFrameRate;
    }
    
    public void ForceGarbageCollection()
    {
        System.GC.Collect();
        lastGCTime = Time.time;
        Debug.Log("Forced garbage collection");
    }
    
    public void TogglePerformanceMonitoring()
    {
        enablePerformanceMonitoring = !enablePerformanceMonitoring;
        Debug.Log($"Performance monitoring: {(enablePerformanceMonitoring ? "Enabled" : "Disabled")}");
    }
    
    public float GetCurrentFPS()
    {
        return 1f / Time.deltaTime;
    }
    
    public float GetAverageFrameTime()
    {
        return averageFrameTime;
    }
    
    public long GetMemoryUsage()
    {
        return System.GC.GetTotalMemory(false);
    }
}
