
$excludedExtensions = @(".dll", ".zip", ".msi", ".exe")
###################################
#
# Retrievce the contents of a powershrell transcript, stripping headers and footers
#
#    param [string] $path: The path to the transript file to read
###################################
function Get-Transcript 
{
   param([string] $path)
   return Get-Content $path |
   Select-String -InputObject {$_} -Pattern "^Start Time\s*:.*" -NotMatch |
   Select-String -InputObject {$_} -Pattern "^End Time\s*:.*" -NotMatch |
   Select-String -InputObject {$_} -Pattern "^Machine\s*:.*" -NotMatch |
   Select-String -InputObject {$_} -Pattern "^Username\s*:.*" -NotMatch |
   Select-String -InputObject {$_} -Pattern "^Transcript started, output file is.*" -NotMatch
}

########################
#
# Get a random file name in the current directory
#
#    param [string] $rootPath: The path of the directory to contain the random file (optional)
########################
function Get-LogFile
{
    param([string] $rootPath = ".")
	return [System.IO.Path]::Combine($rootPath, ([System.IO.Path]::GetRandomFileName()))
}

#################
#
# Execute a test, no exception thrown means the test passes.  Can also be used to compare test 
#  output to a baseline file, or to generate a baseline file
#
#    param [scriptblock] $test: The test code to run
#    param [string] $testScript: The path to the baseline file (optional)
#    param [switch] $generate: Set if the baseline file should be generated, otherwise
#     the baseline file would be used for comparison with test output
##################
function Run-Test 
{
    param([scriptblock]$test, [string] $testScript = $null, [switch] $generate = $false)
	Test-Setup
    $transFile = Get-LogFile "."
	if($testScript)
	{
	    if ($generate)
		{
		    Write-Log "[run-test]: generating script file $testScript"
		    $transFile = $testScript
		}
		else
		{
			Write-Log "[run-test]: writing output to $transFile, using validation script $testScript"
		}
	}
	else
	{
	     Write-Log "[run-test]: Running test without file comparison"
	}
		
	Start-Transcript -Path $transFile	
	$success = $false;
	try 
	{
	  &$test
	  $success = $true;
	}
	finally 
	{
	    Test-Cleanup
	    Stop-Transcript
	    if ($testScript)
		{
		    if ($success -and -not $generate)
		    {
		        $result = Compare-Object (Get-Transcript $testScript) (Get-Transcript $transFile)
			    if ($result -ne $null)
			    {
			        throw "[run-test]: Test Failed " + (Out-String -InputObject $result) + ", Transcript at $transFile"
			    }
			
		    }
		}
		
		if ($success)
		{
		    Write-Log "[run-test]: Test Passed"
		}
	}
	
}

##################
#
# Format a string for proper output to host and transcript
#
#    param [string] $message: The text to write
##################
function Write-Log
{
    [CmdletBinding()]
    param( [Object] [Parameter(Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$false)] $obj = "")
	PROCESS
	{
	    $obj | Out-String | Write-Verbose
	}
}

function Check-SubscriptionMatch
{
    param([string] $baseSubscriptionName, [Microsoft.WindowsAzure.Management.Model.SubscriptionData] $checkedSubscription)
	Write-Log ("[CheckSubscriptionMatch]: base subscription: '$baseSubscriptionName', validating '" + ($checkedSubscription.SubscriptionName)+ "'")
	Format-Subscription $checkedSubscription | Write-Log
	if ($baseSubscriptionName -ne $checkedSubscription.SubscriptionName) 
	{
	    throw ("[Check-SubscriptionMatch]: Subscription Match Failed '" + ($baseSubscriptionName) + "' != '" + ($checkedSubscription.SubscriptionName) + "'")
	}
	
    Write-Log ("CheckSubscriptionMatch]: subscription check succeeded.")
}


##########################
#
# Return the fully qualified filename of a given file
#
#    param [string] $path: The relative path to the file
#
##########################
function Get-FullName
{
    param([string] $path)
	$pathObj = Get-Item $path
	return ($pathObj.FullName)
}

#############################
#
# PowerShell environment setup for running a test, save previous snvironment settings and 
# enable verbose, debug, and warning streams
#
#############################
function Test-Setup
{
    $global:oldConfirmPreference = $global:ConfirmPreference
    $global:oldDebugPreference = $global:DebugPreference
	$global:oldErrorActionPreference = $global:ErrorActionPreference
	$global:oldFormatEnumerationLimit = $global:FormatEnumerationLimit
	$global:oldProgressPreference = $global:ProgressPreference
	$global:oldVerbosePreference = $global:VerbosePreference
	$global:oldWarningPreference = $global:WarningPreference
	$global:oldWhatIfPreference = $global:WhatIfPreference
	$global:ConfirmPreference = "None"
	$global:DebugPreference = "Continue"
	$global:ErrorActionPreference = "Stop"
	$global:FormatEnumerationLimit = 10000
	$global:ProgressPreference = "SilentlyContinue"
    $global:VerbosePreference = "Continue"
	$global:WarningPreference = "Continue"
	$global:WhatIfPreference = 0
}

#############################
#
# PowerShell environment cleanup for running a test, restore previous snvironment settings
#
#############################
function Test-Cleanup
{
     $global:ConfirmPreference = $global:oldConfirmPreference
     $global:DebugPreference = $global:oldDebugPreference
	 $global:ErrorActionPreference = $global:oldErrorActionPreference
	 $global:FormatEnumerationLimit = $global:oldFormatEnumerationLimit
	 $global:ProgressPreference = $global:oldProgressPreference
	 $global:VerbosePreference = $global:oldVerbosePreference
	 $global:WarningPreference = $global:oldWarningPreference
	 $global:WhatIfPreference = $global:oldWhatIfPreference
}

######################
#
# Validate that the given code block throws the given exception
#
#    param [ScriptBlock] $script: The code to test
#    param [string] $message    : The text of the exception that should be thrown
#######################
function Assert-Throws
{
   param([ScriptBlock] $script, [string] $message)
   try 
   {
      &$script
   }
   catch 
   {
       Write-Host ("Caught exception: '" + $_.Exception.Message + "'")
       if ($_.Exception.Message -eq $message)
	   {
	       return $true;
	   }
   }

   throw "Expected exception not received: '$message'";
}

###################
#
# Verify that the given scriptblock returns true
#
#    param [ScriptBlock] $script: The script to execute
#    param [string] $message    : The message to return if the given script does not return true
####################
function Assert-True
{
    param([ScriptBlock] $script, [string]$message)
	
	if (!$message)
	{
	    $message = "Assertion failed: " + $script
	}
	
    $result = &$script
	if (-not $result) 
	{
	    throw $message
	}
	
	return $true
}

######################
#
# Assert that the given file exists
#
#    param [string] $path   : The path to the file to test
#    param [string] $message: The text of the exception to throw if the file doesn't exist
######################
function Assert-Exists
{
    param([string] $path, [string] $message) 
	return Assert-True {Test-Path $path} $message
}

#######################
#
# Dump the contents of a directory to the output stream
#
#    param [string] $rootPath: The path to the directory
#    param [switch] $resurse : True if we should recurse directories
######################
function Dump-Contents
{
    param([string] $rootPath = ".", [switch] $recurse = $false)
    if (-not ((Test-Path $rootPath) -eq $true))
	{
	    throw "[dump-contents]: $rootPath does not exist"
	}
	
	foreach ($item in Get-ChildItem $rootPath)
	{
	    Write-Log
		Write-Log "---------------------------"
	    Write-Log $item.Name
		Write-Log "---------------------------"
		Write-Log
	    if (!$item.PSIsContainer)
		{
		   if (Test-BinaryFile $item)
		   {
		       Write-Log "---- binary data excluded ----"
		   }
		   else
		   {
               Get-Content ($item.PSPath)
		   }
		}
		elseif ($recurse)
		{
		    Dump-Contents ($item.PSPath) -recurse
		}
	}
}

function Test-BinaryFile
{
    param ([System.IO.FileInfo] $file)
	($excludedExtensions | Where-Object -FilterScript {$_ -eq $file.Extension}) -ne $null
}


