@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'ViVeToolEnabler.psm1'

    # Version number of this module.
    ModuleVersion = '1.0.0'

    # Supported PSEditions
    CompatiblePSEditions = @('Desktop', 'Core')

    # ID used to uniquely identify this module
    GUID = '4a8f9c12-3e5b-4c7a-8d1e-9b2f6a7c8d9e'

    # Author of this module
    Author = 'ViVeTool Feature Enabler Team'

    # Company or vendor of this module
    CompanyName = 'Community Open Source'

    # Copyright statement for this module
    Copyright = '(c) 2026. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Automated Windows 11 PowerShell automation tool suite for ViVeTool provisioning, feature management, and rollback.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Minimum version of the .NET Framework required by this module
    DotNetFrameworkVersion = '4.5'

    # Minimum version of the common language runtime (CLR) required by this module
    CLRVersion = '4.0'

    # Functions to export from this module
    FunctionsToExport = @(
        'Ensure-ViVeTool',
        'Invoke-SelfElevation',
        'Test-IsAdministrator',
        'Get-SystemArchitecture',
        'Get-FeatureCatalog',
        'Invoke-ViVeToolFeature',
        'Invoke-FeatureBatch',
        'Write-FeatureLog',
        'Restart-ExplorerProcess',
        'New-RollbackScript'
    )

    # Cmdlets to export from this module
    CmdletsToExport = @()

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module
    AliasesToExport = @()

    # Private data to pass to the module specified in RootModule
    PrivateData = @{
        PSData = @{
            Tags = @('Windows11', 'ViVeTool', 'FeatureManagement', 'Velocity', 'Automation')
            ProjectUri = 'https://github.com/thebookisclosed/ViVe'
            ReleaseNotes = 'Initial 1.0.0 release supporting GA 2025/2026, 26H2, 25H2, and Canary builds.'
        }
    }
}