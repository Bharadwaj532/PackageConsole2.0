Set oShell = CreateObject("WScript.Shell")
If WScript.Arguments.Length = 0 Then
  Set ObjShell = CreateObject("Shell.Application")
  ObjShell.ShellExecute "wscript.exe" _
    , """" & WScript.ScriptFullName & """ RunAsAdministrator", , "runas", 1
  WScript.Quit
End if

strKeyPath = "HKLM\SOFTWARE\Altiris\Client Service\"

ReqDSIp = "DSMEP0722.MS.DS.UHC.COM" ' just need to change this server name in case of changes
ReqTcpAddr = "DSMEP0722.MS.DS.UHC.COM" ' just need to change this server name in case of changes

oShell.RegWrite strKeyPath & "DSIp", ReqDSIp , "REG_SZ"
WScript.Sleep(500)
oShell.RegWrite strKeyPath & "TcpAddr", ReqTcpAddr , "REG_SZ"
WScript.Sleep(500)
Set objWMIService = GetObject("winmgmts:{impersonationLevel=impersonate}!\\.\root\cimv2")
Set colServices = objWMIService.ExecQuery("Select * from Win32_Service where DisplayName = 'Altiris Deployment Agent'")
For Each objService in colServices
	objService.StopService
	WScript.Sleep(10000)
	objService.StartService
Next

DSIpValue = oShell.RegRead("HKLM\SOFTWARE\Altiris\Client Service\DSIp")

If DSIpValue = ReqDSIp Then
msgbox "DSIp is updated to " & ReqDSIp
Else
msgbox "DSIp is NOT updated to " & ReqDSIp
End If

TcpAddrValue = oShell.RegRead("HKLM\SOFTWARE\Altiris\Client Service\TcpAddr")

If TcpAddrValue = ReqTcpAddr Then
msgbox "Dagent is updated to "& ReqTcpAddr
Else
msgbox "Dagent is NOT updated to "& ReqTcpAddr
End If

DSMServer = "APVES68227.MS.DS.UHC.COM" ' just need to change this server name in case of changes

Path = "cmd.exe /C ""C:\Program Files\Altiris\Altiris Agent\AexAgentutil.exe"" /server:"&DSMServer
RTCD = oShell.Run( Path , 1, TRUE)
Set wShell = Nothing'
WScript.Sleep(12000)

SmaAddrValue = oShell.RegRead("HKLM\SOFTWARE\Altiris\Altiris Agent\Servers\")

If SmaAddrValue = DSMServer Then
msgbox "Symantec Management agent is updated to "& DSMServer
Else
msgbox "Symantec Management agent is  failed to update to "& DSMServer
End If