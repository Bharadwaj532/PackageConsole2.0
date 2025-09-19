'==========================================================================
'
' VBScript Source File -- Created with SAPIEN Technologies PrimalScript 5.0.612
'
' NAME: Kurt Jeske
'
' AUTHOR	: UHG , UnitedHealth Group
' DATE  	: 08/3/2011
' Updated	: 12/15/2011
' Version	: See strScannerVersion entry below
'
' COMMENT: Updated to account for 64bit OS, blank display name. Removed exe and migrated everything into the VBS.
'
'==========================================================================

Option Explicit
'On Error Resume Next

Dim objFSO, WshShell, WshNetwork, objWMIService, colOperatingSystems, objOperatingSystem
Dim strCode, strFileName, strComputer, strKey, strSubkey, strBIT, strIDENT, strOS, strVER
Dim objTextFile, objTextFile1, objReg, arrSubkeys, UserName
Dim strEntry1a, strEntry1b, strEntry2, strEntry3, strEntry4, strEntry5, strEntry6, strEntry7, strValue7DRM, strDRM
Dim strValue1, strValue2, strValue5, strValue6, strValue7, intValue3, intValue4, intRet1, btn
Dim strUSERID, strSRNumber, strAVSNumber, strAppName, strAppVer, strDifFileName, btn2, NetworkShare
Dim strCurPath, strEntry8, strScannerVersion, strEntry9, strValueChr1

Set objFSO = CreateObject("Scripting.FileSystemObject")
Set WshShell = CreateObject("Wscript.Shell")
Set WshNetwork = CreateObject("WScript.Network")
strCurPath = CreateObject("Scripting.FileSystemObject").GetAbsolutePathName(".")

strScannerVersion = "3.1"


WshShell.Popup "The registry will be scanned for installed applications. This may take several minutes."_
 & vbCr & vbCr & "Please wait for the process to complete."_
 & vbCr & vbCr & "This process will continue after a 30 second pause.", 30, "ARP scan starting:", 0 + 64
'WScript.Quit

Const HKLM = &H80000002 'HKEY_LOCAL_MACHINE
strComputer = "."
strEntry1a = "DisplayName"
strEntry1b = "QuietDisplayName"
strEntry2 = "DisplayVersion"
strEntry3 = "InstallDate"
strEntry4 = "SystemComponent"
strEntry5 = "Publisher"
strEntry6 = ""
strEntry7 = "Comments"
strEntry8 = "GUID"
strEntry9 = "Scanner Version"

NetworkShare = "\\Nasdslapps001\drm_pkging\ApplicationTeam\ARPScans\"


' Get logged on UserName
UserName = WshShell.ExpandEnvironmentStrings("%USERNAME%")

' Setup logging
If (objFSO.FileExists(".\ARPSCAN.tsv")) Then
 objFSO.DeleteFile ".\ARPSCAN.tsv"
End If
If (objFSO.FileExists(".\After.tsv")) Then
 objFSO.DeleteFile ".\After.tsv"
End If
If (objFSO.FileExists(".\Difference.tsv")) Then
 objFSO.DeleteFile ".\Difference.tsv"
End If

Set objTextFile = objFSO.CreateTextFile(".\ARPSCAN.tsv", True, True)

'Get operating system
   Set objWMIService = GetObject("winmgmts:\\" & strComputer & "\root\cimv2")        
   Set colOperatingSystems = objWMIService.ExecQuery ("Select * from Win32_OperatingSystem")
	For Each objOperatingSystem in colOperatingSystems
		strOS = objOperatingSystem.Caption
		strVER = objOperatingSystem.Version
	Next

'Get Processor Information
strBIT = WshShell.RegRead("HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment\PROCESSOR_ARCHITECTURE")
strIDENT = WshShell.RegRead("HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment\PROCESSOR_IDENTIFIER")

' call subroutine to set keys to be scanned and perform scan
Setkeys

'close report so it can be processed
objTextFile.Close

' Prompt for type and process.
If (objFSO.FileExists("Before.tsv")) Then
 btn = WshShell.Popup("A before scan already exists. Do you want to rerun it?",0,"ISRM ARP Scanner", 3 + 32)
Else
 btn = 6
End If

Select Case btn
' Yes - This is the before scan
	Case 6
		WScript.Echo("The BEFORE scan has been completed."_
		& vbCr & vbCr & "Install the software to be scanned then rerun the scanner and choose NO when prompted.")

		If (objFSO.FileExists("Before.tsv")) Then
 		 objFSO.DeleteFile "Before.tsv"
		End If
		objTextFile.Close
		objFSO.MoveFile "ARPSCAN.tsv" , "Before.tsv"
		WScript.Quit
'Cancel
	Case 2
		WScript.Echo("Cancel was selected, canceling.")
		objTextFile.Close
		objFSO.DeleteFile ".\ARPSCAN.tsv"
		WScript.Quit
' No - This is the after scan
	Case 7
	
	GetInput
	CompareFiles
	
	strDifFileName = strAppName & strAppVer & ".tsv"
	
	If (objFSO.FileExists(strDifFileName)) Then
 	 objFSO.DeleteFile strDifFileName
	End If
	objFSO.MoveFile "Difference.tsv" , strDifFileName
	
	WScript.Echo("Compare completed. The file has been saved as:" & vbCr & vbCr &  strCurPath & "\" & strDifFileName)
	WshShell.Run """" & strDifFileName & """"
	
	
		
	' Send file to the App team share
	PostFile
	WScript.Quit
End Select

'***** Subroutines are below *****

'***** Start - Subroutine to Compare *****
Sub CompareFiles
		objFSO.MoveFile ".\ARPSCAN.tsv" , ".\After.tsv"
		'WScript.Echo("The AFTER scan has been completed." & vbCr & vbCr & "Proceeding to the comparison step.")
		Const ForReading = 1, ForWriting = 2
		Dim obfFSO, txtFile, txtFile1, txtFile2, strLine1, strLine2, strMatch, f
		Set obfFSO = CreateObject("Scripting.FileSystemObject")
		Set txtFile1 = obfFSO.OpenTextFile(".\After.tsv", ForReading,,-1)

			strEntry1a = "DisplayName"
			strEntry1b = "QuietDisplayName"
			strEntry2 = "DisplayVersion"
			strEntry3 = "InstallDate"
			strEntry4 = "SystemComponent"
			strEntry5 = "Publisher"
			strEntry6 = ""
			strEntry7 = "Comments"
			strEntry8 = "GUID"
			strEntry9 = "Scanner Version"

		Set f = obfFSO.OpenTextFile(".\Difference.tsv", ForWriting, True, -1)

		f.WriteLine strEntry1a & vbTab & _
    		strEntry2 & vbTab & _
    		strEntry5 & vbTab & _
    		strEntry3 & vbTab & _
    		"Hidden" & vbtab & _
    		"OS" & vbtab & _
    		"Version" & vbtab & _
    		"Processor_Architecture" & vbTab & _
    		"Processor Identifier" & vbTab & _
   			"Wow6432Node" & vbTab & _
    		strEntry7 & vbTab & _
    		strEntry8 & vbTab & _
    		"SR" & vbTab & _
    		"AVS" & vbTab & _
    		"User ID" & vbTab & _
    		strEntry9


   	Do Until txtFile1.AtEndOfStream
   		strMatch = False
    	strLine1 = txtFile1.Readline
   	Set txtFile2 = obfFSO.OpenTextFile(".\Before.tsv", ForReading,,-1)
        Do Until txtFile2.AtEndOfStream
            strLine2 = txtFile2.Readline
                If Trim(UCase(strLine2)) = Trim(UCase(strLine1)) Then
                    strMatch = True
                Else 
                End If 
        Loop
        txtFile2.Close
        If strMatch <> True then
        'WScript.Echo strLine1
         f.writeline strLine1 & vbTab & strSRNumber & vbTab & strAVSNumber & vbTab & strUSERID & vbTab & strScannerVersion
        End If
   Loop
   objTextFile.Close
   txtFile1.Close
   txtFile2.Close
   f.Close
'   		WScript.Quit
'End Select	

End Sub
'***** End - Subroutine to Compare *****

'***** Start - Subroutine to Set keys *****
Sub Setkeys
' call subroutine to scan "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\"
strKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\"
ScanARP

' call subroutine to scan "SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\"
strKey = "SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\"
On Error Resume Next
WshShell.RegRead ("HKLM\"&strKey)
If Err = 0 Then ScanARP
'ScanARP

End Sub
'***** End - Subroutine to Set keys *****

'***** Start - Subroutine to scan ARP entries *****
Sub ScanARP
Set objReg = GetObject("winmgmts://" & strComputer & _
 "/root/default:StdRegProv")
objReg.EnumKey HKLM, strKey, arrSubkeys

	'strSubkey = Key GUID from array
	'strEntry1a = "DisplayName"
	'strEntry1b = "QuietDisplayName"
	'strEntry2 = "DisplayVersion"
	'strEntry3 = "InstallDate"
	'strEntry4 = "SystemComponent"
	'strEntry5 = "Publisher"
	'strEntry6 = ""
	'strEntry7 = "Comments"
	'strEntry8 = "GUID"
	
'WScript.Echo objReg

For Each strSubkey In arrSubkeys
  intRet1 = objReg.GetStringValue(HKLM, strKey & strSubkey, _
   strEntry1a, strValue1)

'WScript.Echo "DisplayName= " & strValue1

If strValue1 <> "" Then
strValueChr1 = Asc(mid(strValue1,1,1))
'WScript.Echo strValueChr1 & "     " & "DisplayName= " & strValue1
	If strValueChr1 < 48 AND strValueChr1 > 126 Then 
		strValue1 = "***Contains non displayable characters***"
	ElseIf strValueChr1 > 57 And strValueChr1 < 64 Then 
		strValue1 = "***Contains non displayable characters***"
	End If
End If

'  If intRet1 <> 0 Then
'    objReg.GetStringValue HKLM, strKey & strSubkey, _
'     strEntry1b, strValue1
' End If

  objReg.GetStringValue HKLM, strKey & strSubkey, _
   strEntry2, strValue2

  objReg.GetStringValue HKLM, strKey & strSubkey, _
   strEntry3, intValue3

  objReg.GetDWORDValue HKLM, strKey & strSubkey, _
   strEntry4, intValue4

  objReg.GetStringValue HKLM, strKey & strSubkey, _
   strEntry5, strValue5

  objReg.GetStringValue HKLM, strKey & strSubkey, _
   strEntry7, strValue7

' strValue7Left = Left(strValue7,3) 'Left most 20 characters of Comments field  
  strDRM = "DRM"
 If strDRM = Left(strValue7,3) Then 'Left most 3 characters of Comments field are DRM 
  strValue7DRM = strValue7
  Else
  strValue7DRM = ""
 End If


'WScript.Echo(strEntry7 & "= " & strValue7DRM)


' Is it marked as system?
If intValue4 = "1" Then
	intValue4 = "Yes"
	ElseIf intValue4 <> "1" Then
	intValue4 = ""
End If

' Is it from a Wow6432Node?
If strKey = "SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" Then
	strValue6 = "Yes"
End If

  If strValue1 <> "" Then

'WScript.Echo strValue1 & vbtab & _ 
'    strValue2 & vbtab & _
'    strValue5 & vbtab & _ 
'    intValue3 & vbtab & _
'    intValue4 & vbtab & _
'    strOS & vbTab & _
'    strVER & vbTab & _
'    strBIT & vbTab & _
'    strIDENT & vbTab & _
'    strValue6 & vbTab & _
'    strValue7DRM & vbTab & _
'    strSubkey

  objTextFile.WriteLine strValue1 & vbtab & _
    strValue2 & vbTab & _
    strValue5 & vbtab & _
    intValue3 & vbtab & _
    intValue4 & vbtab & _
    strOS & vbTab & _
    strVER & vbTab & _
    strBIT & vbTab & _
    strIDENT & vbTab & _
    strValue6 & vbTab & _
    strValue7DRM & vbTab & _
    strSubkey
  End If 

Next

End Sub
'***** End - Subroutine to scan ARP entries *****

'***** Start - Subroutine to post finished scan *****
Sub PostFile
btn2 = WshShell.Popup("PACKAGERS ONLY" & vbCr & vbCr & "Do you want to copy the report to the share?"_
	& vbCr & vbCr & "Please review the open difference file before selecting YES."_ 
	,0,"Post report", 4 + 32)
Select Case btn2
Case 6
	  objFSO.CopyFile strDifFileName , NetworkShare, True
End Select

End Sub
'***** End - Subroutine to post finished scan *****

'***** Start - Subroutine to get Application information *****
Sub GetInput

strUSERID = InputBox("Enter your ID or Enter if it is correct", "ID", UserName, 500, 500)

' Get Application Name
   Do
   strAppName = InputBox("Enter the application name. This is required.", "Application Name", "", 500, 500)
   Loop Until strAppName <> "" 

' Get Application Version
   Do
   strAppVer = InputBox("Enter the application version. This is required.", "Application Version", "", 500, 500)
   Loop Until strAppVer <> ""

' Get SR#
   Do
   strSRNumber = InputBox("Enter SR number. If there is none then enter 0.", "SR Number", "", 500, 500)
   Loop Until strSRNumber <> "" 

' Get AVS entry
   strAVSNumber = InputBox("Enter the AVS record number if known", "AVS Record Number", "", 500, 500)
	
End Sub
'***** End - Subroutine to get Application information *****
