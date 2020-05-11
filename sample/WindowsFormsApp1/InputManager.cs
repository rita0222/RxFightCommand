using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using SharpDX.DirectInput;

namespace WindowsFormsApp1
{
    public struct KeyInfo
    {
        public char Key;
        public int Duration;
        public int Frame;
        public bool State => Duration == 0;
        public bool IsDirection => '1' <= Key && Key <= '9';
        public bool HasAttribute(char c)
        {
            switch (c)
            {
                case '8':
                    return Key == '7' || Key == '8' || Key == '9';
                case '2':
                    return Key == '1' || Key == '2' || Key == '3';
                case '4':
                    return Key == '7' || Key == '4' || Key == '1';
                case '6':
                    return Key == '9' || Key == '6' || Key == '3';
            }

            throw new Exception();
        }
    }

    public class InputManager
    {
        private Joystick pad;
        private JoystickState prevState = new JoystickState();
        private int directionDuration = 0;
        private int[] buttonsDuration = new int[4];
        private int frame = 0;

        public InputManager()
        {
            DirectInput dinput = new DirectInput();

            var joystickGuid = Guid.Empty;

            // ゲームパッドからゲームパッドを取得する
            if (joystickGuid == Guid.Empty)
            {
                foreach (DeviceInstance device in dinput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                {
                    joystickGuid = device.InstanceGuid;
                    break;
                }
            }

            // ジョイスティックからゲームパッドを取得する
            if (joystickGuid == Guid.Empty)
            {
                foreach (DeviceInstance device in dinput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                {
                    joystickGuid = device.InstanceGuid;
                    break;
                }
            }

            // 見つからなかった場合
            if (joystickGuid == Guid.Empty)
            {
                return;
            }

            // パッド入力周りの初期化
            this.pad = new Joystick(dinput, joystickGuid);
            // バッファサイズを指定
            this.pad.Properties.BufferSize = 128;

            // 相対軸・絶対軸の最小値と最大値を
            // 指定した値の範囲に設定する
            foreach (var deviceObject in this.pad.GetObjects())
            {
                switch (deviceObject.ObjectId.Flags)
                {
                    case DeviceObjectTypeFlags.Axis:
                    // 絶対軸or相対軸
                    case DeviceObjectTypeFlags.AbsoluteAxis:
                    // 絶対軸
                    case DeviceObjectTypeFlags.RelativeAxis:
                        // 相対軸
                        var ir = this.pad.GetObjectPropertiesById(deviceObject.ObjectId);
                        if (ir != null)
                        {
                            try
                            {
                                ir.Range = new InputRange(-1000, 1000);
                            }
                            catch (Exception) { }
                        }
                        break;
                }
            }
        }

        private readonly Subject<KeyInfo> _keyStream = new Subject<KeyInfo>();
        public IObservable<KeyInfo> KeyStream => this._keyStream.AsObservable();

        private static int ToDirection(int x, int y)
        {
            var value = 5;
            if (x > 300) value += 1;
            if (x < -300) value -= 1;
            if (y < -300) value += 3;
            if (y > 300) value -= 3;
            return value;
        }

        public void Update()
        {
            if (this.pad == null) return;

            this.pad.Acquire();
            this.pad.Poll();

            var state = this.pad.GetCurrentState();
            if (state == null) return;

            if (state.X != this.prevState.X || state.Y != this.prevState.Y)
            {
                this._keyStream.OnNext(new KeyInfo
                {
                    Key = ToDirection(this.prevState.X, this.prevState.Y).ToString()[0],
                    Duration = this.directionDuration,
                    Frame = this.frame,
                });
                this._keyStream.OnNext(new KeyInfo
                {
                    Key = ToDirection(state.X, state.Y).ToString()[0],
                    Duration = this.directionDuration = 0,
                    Frame = this.frame,
                });
            }
            else
            {
                ++this.directionDuration;
            }

            char[] buttonName = { 'A', 'B', 'C', 'D' };
            for (int i = 0; i < 4; ++i)
            {
                if (state.Buttons[i] != this.prevState.Buttons[i])
                {
                    this._keyStream.OnNext(new KeyInfo
                    {
                        Key = buttonName[i],
                        Duration = state.Buttons[i] ? 0 : this.buttonsDuration[i],
                        Frame = this.frame,
                    });
                    this.buttonsDuration[i] = 0;
                }

                if (state.Buttons[i])
                {
                    ++this.buttonsDuration[i];
                }
            }

            this.prevState = state;
            ++this.frame;
        }
    }
}
