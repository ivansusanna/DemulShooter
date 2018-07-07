﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace DemulShooter
{
    /*** HeavyFire4 => Heavy Fire Afghanistan ***/
    class Game_HeavyFire3Pc : Game
    {
        private const string FOLDER_GAMEDATA = @"MemoryData\windows";

        private bool _EnableP2 = false;

        /*** MEMORY ADDRESSES **/
        protected int _P1_X_Address;
        protected int _P1_Y_Address;
        protected int _P1_Buttons_Offset;
        protected String _P1_Buttons_NOP_Offset;
        protected int _P2_X_Address;
        protected int _P2_Y_Address;
        protected int _P1_X_Injection_Offset;
        protected int _P1_X_Injection_Return_Offset;
        protected int _P1_Y_Injection_Offset;
        protected int _P1_Y_Injection_Return_Offset;
        protected int _P2_X_Injection_Offset;
        protected int _P2_X_Injection_Return_Offset;
        protected int _P2_Y_Injection_Offset;
        protected int _P2_Y_Injection_Return_Offset;

        //Keys to send
        //For Player 1, hardcoded by the game :
        //Cover Left = A
        //Cover Bottom = S
        //Cover Right = D
        //QTE = Space
        protected byte _P1_QTE_W_VK = Win32.VK_W;
        protected byte _P1_CoverLeft_VK = Win32.VK_A;
        protected byte _P1_CoverRight_VK = Win32.VK_D;
        protected byte _P1_CoverBottom_VK = Win32.VK_S;
        protected byte _P1_QTE_Space_VK = Win32.VK_SPACE;
        //For player 2 if used, keys are choosed for x360kb.ini:
        //I usually prefer to send VirtualKeycodes (less troublesome when no physical Keyboard is plugged)
        //But with x360kb only DIK keycodes are working
        protected short _P2_Trigger_DIK = 0x14; //T
        protected short _P2_Reload_DIK = 0x16;  //Y
        protected short _P2_Grenade_DIK = 015;  //U
        protected short _P2_CoverLeft_DIK = 0x17;  //I
        protected short _P2_CoverDown_DIK = 0x18;  //O
        protected short _P2_CoverRight_DIK = 0x19;  //P

        //Keys to read
        private const byte _P1_Grenade_ScanCode = 0x22;     //[G]

        //Custom data to inject
        protected float _P1_X_Value;
        protected float _P1_Y_Value;
        protected float _P2_X_Value;
        protected float _P2_Y_Value;

        protected float _Axis_X_Min;
        protected float _Axis_X_Max;
        protected float _CoverDelta = 0.3f;
        protected bool _CoverLeftEnabled = false;
        protected bool _CoverBottomEnabled = false;
        protected bool _CoverRightEnabled = false;
        protected bool _QTE_Spacebar_Enabled = false;
        protected bool _QTE_W_Enabled = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public Game_HeavyFire3Pc(string RomName, string GamePath, int CoverSensibility, bool EnableP2, bool Verbose)
            : base()
        {
            GetScreenResolution();

            _RomName = RomName;
            _VerboseEnable = Verbose;
            _ProcessHooked = false;
            _Target_Process_Name = "HeavyFire3";
            if (_RomName.Equals("hfa_s") || _RomName.Equals("hfa2p_s"))
            {
                _Target_Process_Name = "HeavyFire3_Final";
            }

            _EnableP2 = EnableP2;
            _CoverDelta = (float)CoverSensibility / 10.0f;
            WriteLog("Setting Cover delta to screen border to " + _CoverDelta.ToString());

            // To play as Player2 the game needs a Joypad
            // The game is detecting each Aimtrack as additionnal Joypad but unfortunatelly, each detected Joypad HAS TO play.
            // Copying the dinput8.dll file blocks Dinput so no more gamepads with Aimtrak
            // And by using x360kb.ini and xinput1_3.dll in the game's folder, we can add a virtual X360 Joypad to act as player 2
            // Again, to play solo we need to set the x360kb.ini accordingly so that no Joypad is emulated
            try
            {
                using (StreamWriter sw = new StreamWriter(GamePath + @"\x360kb.ini", false))
                {
                    if (_EnableP2)
                        sw.Write(DemulShooter.Properties.Resources.x360kb_hfirea2p);
                    else
                        sw.Write(DemulShooter.Properties.Resources.x360kb_hfirea1p);
                }
                WriteLog("File \"" + GamePath + "\\x360kb.ini\" successfully written !");
            }
            catch (Exception ex)
            {
                WriteLog("Error trying to write file " + GamePath + "\\x360kb.ini\" :" + ex.Message.ToString());
            }

            ReadGameData();            

            _tProcess = new Timer();
            _tProcess.Interval = 500;
            _tProcess.Tick += new EventHandler(tProcess_Tick);
            _tProcess.Enabled = true;
            _tProcess.Start();

            WriteLog("Waiting for Windows Game " + _RomName + " game to hook.....");
        }

        /// <summary>
        /// Timer event when looking for Process (auto-Hook and auto-close)
        /// </summary>
        private void tProcess_Tick(Object Sender, EventArgs e)
        {
            if (!_ProcessHooked)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(_Target_Process_Name);
                    if (processes.Length > 0)
                    {
                        _TargetProcess = processes[0];
                        _ProcessHandle = _TargetProcess.Handle;
                        _TargetProcess_MemoryBaseAddress = _TargetProcess.MainModule.BaseAddress;

                        if (_TargetProcess_MemoryBaseAddress != IntPtr.Zero)
                        {
                            _ProcessHooked = true;
                            WriteLog("Attached to Process " + _Target_Process_Name + ".exe, ProcessHandle = " + _ProcessHandle);
                            WriteLog(_Target_Process_Name + ".exe = 0x" + _TargetProcess_MemoryBaseAddress.ToString("X8"));
                                                                                    
                            SetHack();
                            ApplyKeyboardHook();
                        }
                    }
                }
                catch
                {
                    WriteLog("Error trying to hook " + _Target_Process_Name + ".exe");
                }
            }
            else
            {
                Process[] processes = Process.GetProcessesByName(_Target_Process_Name);
                if (processes.Length <= 0)
                {
                    _ProcessHooked = false;
                    _TargetProcess = null;
                    _ProcessHandle = IntPtr.Zero;
                    _TargetProcess_MemoryBaseAddress = IntPtr.Zero;
                    WriteLog(_Target_Process_Name + ".exe closed");
                    Environment.Exit(0);
                }
            }
        }

        #region File I/O

        /// <summary>
        /// Read memory values in .cfg file
        /// </summary>
        protected override void ReadGameData()
        {
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\" + FOLDER_GAMEDATA + @"\" + _RomName + ".cfg"))
            {
                using (StreamReader sr = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + @"\" + FOLDER_GAMEDATA + @"\" + _RomName + ".cfg"))
                {
                    string line;
                    line = sr.ReadLine();
                    while (line != null)
                    {
                        string[] buffer = line.Split('=');
                        if (buffer.Length > 1)
                        {
                            try
                            {
                                switch (buffer[0].ToUpper().Trim())
                                {
                                    case "P1_X_INJECTION_OFFSET":
                                        _P1_X_Injection_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P1_X_INJECTION_RETURN_OFFSET":
                                        _P1_X_Injection_Return_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P1_Y_INJECTION_OFFSET":
                                        _P1_Y_Injection_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P1_Y_INJECTION_RETURN_OFFSET":
                                        _P1_Y_Injection_Return_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P2_X_INJECTION_OFFSET":
                                        _P2_X_Injection_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P2_X_INJECTION_RETURN_OFFSET":
                                        _P2_X_Injection_Return_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P2_Y_INJECTION_OFFSET":
                                        _P2_Y_Injection_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P2_Y_INJECTION_RETURN_OFFSET":
                                        _P2_Y_Injection_Return_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P1_BUTTONS_OFFSET":
                                        _P1_Buttons_Offset = int.Parse(buffer[1].Substring(3).Trim(), NumberStyles.HexNumber);
                                        break;
                                    case "P1_BUTTONS_NOP_OFFSET":
                                        _P1_Buttons_NOP_Offset = buffer[1].Trim();
                                        break;
                                    default: break;
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteLog("Error reading game data : " + ex.Message.ToString());
                            }
                        }
                        line = sr.ReadLine();
                    }
                    sr.Close();
                }
            }
            else
            {
                WriteLog("File not found : " + AppDomain.CurrentDomain.BaseDirectory + @"\" + FOLDER_GAMEDATA + @"\" + _RomName + ".cfg");
            }
        }

        #endregion

        #region Screen

        public override bool GameScale(MouseInfo Mouse, int Player)
        {
            if (_ProcessHandle != IntPtr.Zero)
            {
                try
                {
                    Win32.Rect TotalRes = new Win32.Rect();
                    Win32.GetClientRect(_TargetProcess.MainWindowHandle, ref TotalRes);
                    int TotalResX = TotalRes.Right - TotalRes.Left;
                    int TotalResY = TotalRes.Bottom - TotalRes.Top;

                    WriteLog("Game client window resolution (Px) = [ " + TotalResX + "x" + TotalResY + " ]");

                    //Y => [-1 ; 1] float
                    //X => depend on ration Width/Height (ex : [-1.7777; 1.7777] with 1920x1080)

                    float fRatio = (float)TotalResX / (float)TotalResY;
                    _Axis_X_Min = -fRatio;
                    _Axis_X_Max = fRatio;

                    float _Y_Value = (2.0f * Mouse.pTarget.Y / TotalResY) - 1.0f;
                    float _X_Value = (fRatio * 2 * Mouse.pTarget.X / TotalResX) - fRatio;

                    if (_X_Value < _Axis_X_Min)
                        _X_Value = _Axis_X_Min;
                    if (_Y_Value < -1.0f)
                        _Y_Value = -1.0f;
                    if (_X_Value > _Axis_X_Max)
                        _X_Value = _Axis_X_Max;
                    if (_Y_Value > 1.0f)
                        _Y_Value = 1.0f;

                    if (Player == 1)
                    {
                        _P1_X_Value = _X_Value;
                        _P1_Y_Value = _Y_Value;
                    }
                    else if (Player == 2)
                    {
                        _P2_X_Value = _X_Value;
                        _P2_Y_Value = _Y_Value;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    WriteLog("Error scaling mouse coordonates to GameFormat : " + ex.Message.ToString());
                }
            }
            return false;
        }

        #endregion

        #region MemoryHack

        private void SetHack()
        {
            SetHack_Data();
            SetHack_P1X();
            SetHack_P1Y();
            SetHack_P2X();
            SetHack_P2Y();
            SetNops((int)_TargetProcess_MemoryBaseAddress, _P1_Buttons_NOP_Offset);
            WriteLog("Memory Hack complete !");
            WriteLog("-");
        }

        /*** Creating a custom memory bank to store our data ***/
        private void SetHack_Data()
        {
            //1st Codecave : storing our Axis Data
            Memory DataCaveMemory = new Memory(_TargetProcess, _TargetProcess.MainModule.BaseAddress);
            DataCaveMemory.Open();
            DataCaveMemory.Alloc(0x800);

            _P1_X_Address = DataCaveMemory.CaveAddress;
            _P1_Y_Address = DataCaveMemory.CaveAddress + 0x04;

            _P2_X_Address = DataCaveMemory.CaveAddress + 0x20;
            _P2_Y_Address = DataCaveMemory.CaveAddress + 0x24;

            WriteLog("Custom data will be stored at : 0x" + _P1_X_Address.ToString("X8"));
        }

        /// <summary>
        /// All Axis codecave are the same :
        /// The game use some fstp [XXX] instruction, but we can't just NOP it as graphical glitches may appear.
        /// So we just add another set of instructions instruction immediatelly after to change the register 
        /// to our own desired value
        /// </summary>
        private void SetHack_P1X()
        {
            List<Byte> Buffer = new List<Byte>();
            Memory CaveMemory = new Memory(_TargetProcess, _TargetProcess.MainModule.BaseAddress);
            CaveMemory.Open();
            CaveMemory.Alloc(0x800);

            //fstp [esi+edi*8+F0000000]
            CaveMemory.Write_StrBytes("D9 9C FE F0 00 00 00");
            //push eax
            CaveMemory.Write_StrBytes("50");
            //mov eax, [P1_X]
            CaveMemory.Write_StrBytes("A1");
            byte[] b = BitConverter.GetBytes(_P1_X_Address);
            CaveMemory.Write_Bytes(b);
            //mov [esi+edi*8+F000000], eax
            CaveMemory.Write_StrBytes("89 84 FE F0 00 00 00");
            //pop eax
            CaveMemory.Write_StrBytes("58");
            CaveMemory.Write_jmp((int)_TargetProcess.MainModule.BaseAddress + _P1_X_Injection_Return_Offset);

            WriteLog("Adding P1 X Axis Codecave at : 0x" + CaveMemory.CaveAddress.ToString("X8"));

            //Code injection
            IntPtr ProcessHandle = _TargetProcess.Handle;
            int bytesWritten = 0;
            int jumpTo = 0;
            jumpTo = CaveMemory.CaveAddress - ((int)_TargetProcess.MainModule.BaseAddress + _P1_X_Injection_Offset) - 5;
            Buffer = new List<byte>();
            Buffer.Add(0xE9);
            Buffer.AddRange(BitConverter.GetBytes(jumpTo));
            Buffer.Add(0x90);
            Buffer.Add(0x90);
            Win32.WriteProcessMemory((int)ProcessHandle, (int)_TargetProcess.MainModule.BaseAddress + _P1_X_Injection_Offset, Buffer.ToArray(), Buffer.Count, ref bytesWritten);
        }

        private void SetHack_P1Y()
        {
            List<Byte> Buffer = new List<Byte>();
            Memory CaveMemory = new Memory(_TargetProcess, _TargetProcess.MainModule.BaseAddress);
            CaveMemory.Open();
            CaveMemory.Alloc(0x800);

            //fstp [esi+edi*8+F4000000]
            CaveMemory.Write_StrBytes("D9 9C FE F4 00 00 00");
            //push eax
            CaveMemory.Write_StrBytes("50");
            //mov eax, [P1_Y]
            CaveMemory.Write_StrBytes("A1");
            byte[] b = BitConverter.GetBytes(_P1_Y_Address);
            CaveMemory.Write_Bytes(b);
            //mov [esi+edi*8+F400000], eax
            CaveMemory.Write_StrBytes("89 84 FE F4 00 00 00");
            //pop eax
            CaveMemory.Write_StrBytes("58");
            CaveMemory.Write_jmp((int)_TargetProcess.MainModule.BaseAddress + _P1_Y_Injection_Return_Offset);

            WriteLog("Adding P1 Y Axis Codecave at : 0x" + CaveMemory.CaveAddress.ToString("X8"));

            //Code injection
            IntPtr ProcessHandle = _TargetProcess.Handle;
            int bytesWritten = 0;
            int jumpTo = 0;
            jumpTo = CaveMemory.CaveAddress - ((int)_TargetProcess.MainModule.BaseAddress + _P1_Y_Injection_Offset) - 5;
            Buffer = new List<byte>();
            Buffer.Add(0xE9);
            Buffer.AddRange(BitConverter.GetBytes(jumpTo));
            Buffer.Add(0x90);
            Buffer.Add(0x90);
            Win32.WriteProcessMemory((int)ProcessHandle, (int)_TargetProcess.MainModule.BaseAddress + _P1_Y_Injection_Offset, Buffer.ToArray(), Buffer.Count, ref bytesWritten);
        }

        private void SetHack_P2X()
        {
            List<Byte> Buffer = new List<Byte>();
            Memory CaveMemory = new Memory(_TargetProcess, _TargetProcess.MainModule.BaseAddress);
            CaveMemory.Open();
            CaveMemory.Alloc(0x800);

            //fstp [esi+edi*8+F0000000]
            CaveMemory.Write_StrBytes("D9 9C FE F0 00 00 00");
            //push eax
            CaveMemory.Write_StrBytes("50");
            //mov eax, [P2_X]
            CaveMemory.Write_StrBytes("A1");
            byte[] b = BitConverter.GetBytes(_P2_X_Address);
            CaveMemory.Write_Bytes(b);
            //mov [esi+edi*8+F000000], eax
            CaveMemory.Write_StrBytes("89 84 FE F0 00 00 00");
            //pop eax
            CaveMemory.Write_StrBytes("58");
            CaveMemory.Write_jmp((int)_TargetProcess.MainModule.BaseAddress + _P2_X_Injection_Return_Offset);

            WriteLog("Adding P2 X Axis Codecave at : 0x" + CaveMemory.CaveAddress.ToString("X8"));

            //Code injection
            IntPtr ProcessHandle = _TargetProcess.Handle;
            int bytesWritten = 0;
            int jumpTo = 0;
            jumpTo = CaveMemory.CaveAddress - ((int)_TargetProcess.MainModule.BaseAddress + _P2_X_Injection_Offset) - 5;
            Buffer = new List<byte>();
            Buffer.Add(0xE9);
            Buffer.AddRange(BitConverter.GetBytes(jumpTo));
            Buffer.Add(0x90);
            Buffer.Add(0x90);
            Win32.WriteProcessMemory((int)ProcessHandle, (int)_TargetProcess.MainModule.BaseAddress + _P2_X_Injection_Offset, Buffer.ToArray(), Buffer.Count, ref bytesWritten);
        }

        private void SetHack_P2Y()
        {
            List<Byte> Buffer = new List<Byte>();
            Memory CaveMemory = new Memory(_TargetProcess, _TargetProcess.MainModule.BaseAddress);
            CaveMemory.Open();
            CaveMemory.Alloc(0x800);

            //fstp [esi+edi*8+F4000000]
            CaveMemory.Write_StrBytes("D9 9C FE F4 00 00 00");
            //push eax
            CaveMemory.Write_StrBytes("50");
            //mov eax, [P2_Y]
            CaveMemory.Write_StrBytes("A1");
            byte[] b = BitConverter.GetBytes(_P2_Y_Address);
            CaveMemory.Write_Bytes(b);
            //mov [esi+edi*8+F400000], eax
            CaveMemory.Write_StrBytes("89 84 FE F4 00 00 00");
            //pop eax
            CaveMemory.Write_StrBytes("58");
            CaveMemory.Write_jmp((int)_TargetProcess.MainModule.BaseAddress + _P2_Y_Injection_Return_Offset);

            WriteLog("Adding P2 Y Axis Codecave at : 0x" + CaveMemory.CaveAddress.ToString("X8"));

            //Code injection
            IntPtr ProcessHandle = _TargetProcess.Handle;
            int bytesWritten = 0;
            int jumpTo = 0;
            jumpTo = CaveMemory.CaveAddress - ((int)_TargetProcess.MainModule.BaseAddress + _P2_Y_Injection_Offset) - 5;
            Buffer = new List<byte>();
            Buffer.Add(0xE9);
            Buffer.AddRange(BitConverter.GetBytes(jumpTo));
            Buffer.Add(0x90);
            Buffer.Add(0x90);
            Win32.WriteProcessMemory((int)ProcessHandle, (int)_TargetProcess.MainModule.BaseAddress + _P2_Y_Injection_Offset, Buffer.ToArray(), Buffer.Count, ref bytesWritten);
        }

        public override void SendInput(MouseInfo mouse, int Player)
        {
            if (Player == 1)
            {
                //Setting Values in memory for the Codecave to read it
                byte[] buffer = BitConverter.GetBytes(_P1_X_Value);
                WriteBytes(_P1_X_Address, buffer);
                buffer = BitConverter.GetBytes(_P1_Y_Value);
                WriteBytes(_P1_Y_Address, buffer);

                //Inputs
                if (mouse.button == Win32.RI_MOUSE_LEFT_BUTTON_DOWN)
                {
                    Apply_OR_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_Buttons_Offset, 0x01);
                }
                else if (mouse.button == Win32.RI_MOUSE_LEFT_BUTTON_UP)
                {
                    Apply_AND_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_Buttons_Offset, 0xFE);
                }
                else if (mouse.button == Win32.RI_MOUSE_MIDDLE_BUTTON_DOWN)
                {
                    //If the player is aiming on the left side of the screen before pressing the button
                    //=> Cover Left
                    if (_P1_X_Value < _Axis_X_Min + _CoverDelta)
                    {
                        Send_VK_KeyDown(_P1_CoverLeft_VK);
                        _CoverLeftEnabled = true;
                    }
                    //If the player is aiming on the bottom side of the screen before pressing the button
                    //=> Cover Down
                    else if (_P1_Y_Value > (1.0f - _CoverDelta))
                    {
                        Send_VK_KeyDown(_P1_CoverBottom_VK);
                        _CoverBottomEnabled = true;
                    }
                    //If the player is aiming on the right side of the screen before pressing the button
                    //=> Cover Right
                    else if (_P1_X_Value > _Axis_X_Max - _CoverDelta)
                    {
                        Send_VK_KeyDown(_P1_CoverRight_VK);
                        _CoverRightEnabled = true;
                    }
                    //If the player is aiming on the top side of the screen before pressing the button
                    //=> W [QTE]
                    else if (_P1_Y_Value < (-1.0f + _CoverDelta))
                    {
                        Send_VK_KeyDown(_P1_QTE_W_VK);
                        _QTE_W_Enabled = true;
                    }
                    //If nothing above
                    //=> Spacebar [QTE]
                    else
                    {
                        Send_VK_KeyDown(_P1_QTE_Space_VK);
                        _QTE_Spacebar_Enabled = true;
                    }
                }
                else if (mouse.button == Win32.RI_MOUSE_MIDDLE_BUTTON_UP)
                {
                    if (_CoverLeftEnabled)
                    {
                        Send_VK_KeyUp(_P1_CoverLeft_VK);
                        _CoverLeftEnabled = false;
                    }
                    if (_CoverBottomEnabled)
                    {
                        Send_VK_KeyUp(_P1_CoverBottom_VK);
                        _CoverBottomEnabled = false;
                    }
                    if (_CoverRightEnabled)
                    {
                        Send_VK_KeyUp(_P1_CoverRight_VK);
                        _CoverRightEnabled = false;
                    }
                    if (_QTE_W_Enabled)
                    {
                        Send_VK_KeyUp(_P1_QTE_W_VK);
                        _QTE_W_Enabled = false;
                    }
                    if (_QTE_Spacebar_Enabled)
                    {
                        Send_VK_KeyUp(_P1_QTE_Space_VK);
                        _QTE_Spacebar_Enabled = false;
                    }
                }
                else if (mouse.button == Win32.RI_MOUSE_RIGHT_BUTTON_DOWN)
                {
                    Apply_OR_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_Buttons_Offset, 0x02);
                }
                else if (mouse.button == Win32.RI_MOUSE_RIGHT_BUTTON_UP)
                {
                    Apply_AND_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_Buttons_Offset, 0xFD);
                }
            }
            else if (Player == 2)
            {
                //Setting Values in memory for the Codecave to read it
                byte[] buffer = BitConverter.GetBytes(_P2_X_Value);
                WriteBytes(_P2_X_Address, buffer);
                buffer = BitConverter.GetBytes(_P2_Y_Value);
                WriteBytes(_P2_Y_Address, buffer);


                // Player 2 buttons are simulated by x360kb.ini so we just send needed Keyboard strokes
                if (mouse.button == Win32.RI_MOUSE_LEFT_BUTTON_DOWN)
                {
                    SendKeyDown(_P2_Trigger_DIK);
                }
                else if (mouse.button == Win32.RI_MOUSE_LEFT_BUTTON_UP)
                {
                    SendKeyUp(_P2_Trigger_DIK);
                }
                else if (mouse.button == Win32.RI_MOUSE_MIDDLE_BUTTON_DOWN)
                {
                    if (_P2_X_Value < _Axis_X_Min + _CoverDelta)
                    {
                        SendKeyDown(_P2_CoverLeft_DIK);
                    }
                    else if (_P2_Y_Value > (1.0f - _CoverDelta))
                    {
                        SendKeyDown(_P2_CoverDown_DIK);
                    }
                    else if (_P2_X_Value > _Axis_X_Max - _CoverDelta)
                    {
                        SendKeyDown(_P2_CoverRight_DIK);
                    }
                }
                else if (mouse.button == Win32.RI_MOUSE_MIDDLE_BUTTON_UP)
                {
                    SendKeyUp(_P2_CoverLeft_DIK);
                    SendKeyUp(_P2_CoverDown_DIK);
                    SendKeyUp(_P2_CoverRight_DIK);

                }
                else if (mouse.button == Win32.RI_MOUSE_RIGHT_BUTTON_DOWN)
                {
                    SendKeyDown(_P2_Reload_DIK);
                }
                else if (mouse.button == Win32.RI_MOUSE_RIGHT_BUTTON_UP)
                {
                    SendKeyUp(_P2_Reload_DIK);
                }
            }            
        }

        // Keyboard will be used to use Grenade
        protected override IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                Win32.KBDLLHOOKSTRUCT s = (Win32.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32.KBDLLHOOKSTRUCT));
                if ((UInt32)wParam == Win32.WM_KEYDOWN)
                {
                    switch (s.scanCode)
                    {
                        case _P1_Grenade_ScanCode:
                            {
                                Apply_OR_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_Buttons_Offset, 0x04);
                            } break;
                        default:
                            break;
                    }
                }
                else if ((UInt32)wParam == Win32.WM_KEYUP)
                {
                    switch (s.scanCode)
                    {
                        case _P1_Grenade_ScanCode:
                            {
                                Apply_AND_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_Buttons_Offset, 0xFB);
                            } break;
                        default:
                            break;
                    }
                }
            }
            return Win32.CallNextHookEx(_KeyboardHookID, nCode, wParam, lParam);
        }

        #endregion
    }
}
