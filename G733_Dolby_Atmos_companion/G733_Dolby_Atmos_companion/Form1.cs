using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HidLibrary;
using System.Diagnostics;
using System.Threading;

namespace G733_Dolby_Atmos_companion
{
    public partial class Form1 : Form
    {
        private static HidDevice HidDevice;
        HidDevice[] HidDeviceList;
        private bool device_selected = false;

        private const byte top = 0x01;
        private const byte bottom = 0x00;
        private const byte both = 0x02;
        private const byte off = 0x00;
        private const byte fix = 0x01;
        private const byte breathing = 0x02;
        private const byte cycling = 0x03;

        private int voltage = 0;
        private bool is_headset_connected = false;

        Graphics canvas;
        Bitmap iconBitmap = new Bitmap(32, 32);
        Color backup2 = Color.FromArgb(255, 0, 76);
        StringFormat format = new StringFormat();
        int percent = 0;
        double estim = 0;
        HidDeviceData data;

        public Form1()
        {
            InitializeComponent();
            colorDialog1.AllowFullOpen = true;
            pictureBox1.BackColor = colorDialog1.Color;
            notifyIcon1.Visible = true;
            notifyIcon1.Text = "Disconnected";

            label1.Text = trackBar1.Value.ToString() + " ms";
            label2.Text = trackBar_brightness.Value.ToString() + " %";
            label6.Text = trackBar2.Value.ToString() + " %";
            label11.Text = "";

            trackBar1.Value = Properties.Settings.Default.light_duration;
            trackBar2.Value = Properties.Settings.Default.sidetone;
            trackBar_brightness.Value = Properties.Settings.Default.light_brightness;

            Color backup = Color.FromArgb(Properties.Settings.Default.color_R, Properties.Settings.Default.color_G, Properties.Settings.Default.color_B);

            pictureBox1.BackColor = backup;

            switch (Properties.Settings.Default.light_mode)
            {
                case off: radioButton_off.Checked = true;
                    break;
                case fix: radioButton_fixed.Checked = true;
                    break;
                case breathing: radioButton_breathing.Checked = true;
                    break;
                case cycling: radioButton_cycling.Checked = true;
                    break;
                default: break;
            }
            switch (Properties.Settings.Default.light_side)
            {
                case both: radioButton_both.Checked = true;
                    break;
                case top: radioButton_top.Checked = true;
                    break;
                case bottom: radioButton_down.Checked = true;
                    break;
                default: break;
            }

            canvas = Graphics.FromImage(iconBitmap);
            format.Alignment = StringAlignment.Center;

            notify_percentage();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (HidDevice != null)
            {
                set_lights();
            }
            else
            {
                Console.WriteLine("Could not find Headset.");
                return;
            }
        }

        private void set_lights()
        {
            byte[] OutData = new byte[HidDevice.Capabilities.OutputReportByteLength];
            bool status;

            byte side = Properties.Settings.Default.light_side;
            byte mode = Properties.Settings.Default.light_mode;
            byte duration_msb = (byte)((Properties.Settings.Default.light_duration & 0xff00)>>8);
            byte duration_lsb = (byte)(Properties.Settings.Default.light_duration & 0xff);
            byte brightness = Properties.Settings.Default.light_brightness;

            int r = Properties.Settings.Default.color_R;
            int g = Properties.Settings.Default.color_G;
            int b = Properties.Settings.Default.color_B;

            r = (int)(-r * 0.0086 + r * r * 0.0028 + r * r * r * 0.000004);
            g = (int)(-g * 0.0086 + g * g * 0.0028 + g * g * g * 0.000004);
            b = (int)(-b * 0.0086 + b * b * 0.0028 + b * b * b * 0.000004);

            for (int index = 0; index < (HidDevice.Capabilities.OutputReportByteLength); ++index)
            {
                OutData[index] = 0x00;
            }

            OutData[0] = 0x11;
            OutData[1] = 0xff;

            OutData[2] = 0x04;
            OutData[3] = 0x03e;

            OutData[4] = side;
            OutData[5] = mode;

            switch (mode)
            {
                case off:
                    break;
                case fix:
                    Console.WriteLine("Send fixed color: " + (byte)r + "/" + (byte)g + "/" + (byte)b);
                    //bytes 6,7,8 color
                    OutData[6] = (byte)r;
                    OutData[7] = (byte)g;
                    OutData[8] = (byte)b;
                    OutData[9] = 0x02; 
                    break;
                case breathing:
                    //bytes 6,7,8 color
                    OutData[6] = (byte)r;
                    OutData[7] = (byte)g;
                    OutData[8] = (byte)b;
                    //bytes 9,10 duration
                    OutData[9] = duration_msb;
                    OutData[10] = duration_lsb;
                    //byte 12 brightness
                    OutData[12] = brightness;
                    break;
                case cycling:
                    OutData[9] = 0x00;
                    //bytes 10,11 duration
                    OutData[11] = duration_msb;
                    OutData[12] = duration_lsb;
                    //byte 13 brightness
                    OutData[13] = brightness;
                    break;
                default: return;
            }

            if (side != both)
            {
                status = HidDevice.Write(OutData);
                Console.WriteLine("both side light frame sent: " + status);
                Console.WriteLine("Send hid frame: " + BitConverter.ToString(OutData));
            }
            else
            {
                OutData[4] = bottom;
                status = HidDevice.Write(OutData);
                Console.WriteLine("bottom side light frame sent: " + status);
                Console.WriteLine("Send hid frame: " + BitConverter.ToString(OutData));
                OutData[4] = top;
                status = HidDevice.Write(OutData);
                Console.WriteLine("top side light frame sent: " + status);
                Console.WriteLine("Send hid frame: " + BitConverter.ToString(OutData));
            }
        }

        private void DeviceAttachedHandler()
        {
            Console.WriteLine("Headset attached.");
        }

        private void DeviceRemovedHandler()
        {
            Console.WriteLine("Headset removed.");
            HidDevice.CloseDevice();
            HidDevice.Inserted -= DeviceAttachedHandler;
            HidDevice.Removed -= DeviceRemovedHandler;

            backgroundWorker1.CancelAsync();
            HID_read_timer.Enabled = false;
            battery_level_update_timer.Enabled = false;
        }

        private void headset_connected_check_Tick(object sender, EventArgs e)
        {
            notify_percentage();
            if (HidDevice != null) return;

            HidDeviceList = HidDevices.Enumerate(0x046d, 0x0ab5).ToArray();

            for (int i = 0; i < HidDeviceList.Length; i++)
            {
                Console.WriteLine(HidDeviceList[i]);
                HidDeviceList[i].OpenDevice();
                Console.WriteLine("Connected: " + HidDeviceList[i].IsConnected.ToString());
                Console.WriteLine("InputReportByteLength: " + HidDeviceList[i].Capabilities.InputReportByteLength);
                Console.WriteLine("OutputReportByteLength: " + HidDeviceList[i].Capabilities.OutputReportByteLength);
                Console.WriteLine("FeatureReportByteLength: " + HidDeviceList[i].Capabilities.FeatureReportByteLength);
                Console.WriteLine("DevicePath: " + HidDeviceList[i].DevicePath);
                Console.WriteLine("Description: " + HidDeviceList[i].Description);
                Console.WriteLine("ReadHandle: " + HidDeviceList[i].ReadHandle);
                Console.WriteLine("");
                if (HidDeviceList[i].Capabilities.OutputReportByteLength == 20 && HidDeviceList[i].Capabilities.InputReportByteLength == 20)
                {
                    device_selected = true;
                    HidDevice = HidDeviceList[i];
                    break;
                }

                HidDeviceList[i].CloseDevice();
            }

            if (!device_selected) return;

            HidDevice.Inserted += DeviceAttachedHandler;
            HidDevice.Removed += DeviceRemovedHandler;

            HidDevice.MonitorDeviceEvents = true;

            //Start hid frame read process
            backgroundWorker1.RunWorkerAsync();

            //Start USB Read Timer (Well, it's a hack now, one more ... to detect headset)
            HID_read_timer.Enabled = true;

            //Start battery update Timer (every 60sec)
            battery_level_update_timer.Enabled = true;
            battery_level_update();
            
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            trackBar1.Value = (trackBar1.Value / 100) * 100;
        }

        private void trackBar_brightness_Scroll(object sender, EventArgs e)
        {
            trackBar_brightness.Value = (trackBar_brightness.Value / 5) * 5;     
        }

        private void radioButton_fixed_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.light_mode = fix;
            Properties.Settings.Default.Save();
        }

        private void radioButton_breathing_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.light_mode = breathing;
            Properties.Settings.Default.Save();
        }

        private void radioButton_cycling_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.light_mode = cycling;
            Properties.Settings.Default.Save();
        }

        private void radioButton_off_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.light_mode = off;
            Properties.Settings.Default.Save();
        }

        private void radioButton_both_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.light_side = both;
            Properties.Settings.Default.Save();
        }

        private void radioButton_top_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.light_side = top;
            Properties.Settings.Default.Save();
        }

        private void radioButton_down_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.light_side = bottom;
            Properties.Settings.Default.Save();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            colorDialog1.ShowDialog();
            Console.WriteLine(colorDialog1.Color);
            pictureBox1.BackColor = colorDialog1.Color;
            Properties.Settings.Default.color_R = colorDialog1.Color.R;
            Properties.Settings.Default.color_G = colorDialog1.Color.G;
            Properties.Settings.Default.color_B = colorDialog1.Color.B;
            Properties.Settings.Default.Save();
        }

        private void trackBar2_MouseCaptureChanged(object sender, EventArgs e)
        {
            sidetone_set();
        }

        private void sidetone_set()
        {
            byte[] OutData = new byte[HidDevice.Capabilities.OutputReportByteLength];
            bool status;

            for (int index = 0; index < (HidDevice.Capabilities.OutputReportByteLength); ++index)
            {
                OutData[index] = 0x00;
            }

            OutData[0] = 0x11;
            OutData[1] = 0xff;

            OutData[2] = 0x07;
            OutData[3] = 0x1e;

            OutData[4] = Properties.Settings.Default.sidetone;

            status = HidDevice.Write(OutData);
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
        }

        private void battery_level_update_timer_Tick(object sender, EventArgs e)
        {
            battery_level_update();
        }

        private void battery_level_update()
        {
            byte[] OutData = new byte[HidDevice.Capabilities.OutputReportByteLength];

            bool status;

            for (int index = 0; index < (HidDevice.Capabilities.OutputReportByteLength); ++index)
            {
                OutData[index] = 0x00;
            }

            OutData[0] = 0x11;
            OutData[1] = 0xff;

            OutData[2] = 0x08;
            OutData[3] = 0x0f;

            //Ask for battery voltage
            status = HidDevice.Write(OutData);
        }

        private void HID_read_timer_Tick(object sender, EventArgs e)
        {
            if (voltage == 0) label11.Text = "Disconnected";
            else label11.Text = voltage.ToString() + " mV";

            if (is_headset_connected == true)
            {
                pictureBox2.BackColor = Color.Green;
            }
            else
            {
                pictureBox2.BackColor = Color.Red;
                notifyIcon1.Text = "Disconnected";
            }
        }

        private void process_frame(HidDeviceData report)
        {
            Console.WriteLine("Received hid frame: " + BitConverter.ToString(report.Data));

            if (report.Data[2] == 0x08 && ((report.Data[3] & 0xf0) == 0x00))
            {
                voltage = report.Data[4] << 8;
                voltage |= report.Data[5];
                Console.WriteLine("Battery voltage: " + voltage);

                if (voltage == 0)
                {
                    is_headset_connected = false;
                }
                else if (is_headset_connected == false)
                {
                    is_headset_connected = true;
                    Console.WriteLine("Start timer of the shame");
                    BeginInvoke(new EventHandler(delegate
                    {
                        timer_of_the_shame.Enabled = true;
                    }));
                }
            }
            if (report.Data[2] == 0x07 && ((report.Data[3] & 0xf0) == 0x10))
            {
                Console.WriteLine("Sidetone headset level: " + report.Data[4]);
                Properties.Settings.Default.sidetone = report.Data[4];
                Properties.Settings.Default.Save();
            }   
        }

        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {
            label6.Text = trackBar2.Value.ToString() + " %";
            Properties.Settings.Default.sidetone = (byte)trackBar2.Value;
            Properties.Settings.Default.Save();
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            label1.Text = trackBar1.Value.ToString() + " ms";
            Properties.Settings.Default.light_duration = trackBar1.Value;
            Properties.Settings.Default.Save();
        }

        private void trackBar_brightness_ValueChanged(object sender, EventArgs e)
        {
            label2.Text = trackBar_brightness.Value.ToString() + " %";
            Properties.Settings.Default.light_brightness = (byte)trackBar_brightness.Value;
            Properties.Settings.Default.Save();
        }

        private void timer_of_the_shame_Tick(object sender, EventArgs e)
        {
            //Some shitty hack, wanted to use a background worker but ... way too much effort to achieve this.
            Console.WriteLine("Timer of the shame started");
            set_lights();
            trackBar2.Value = Properties.Settings.Default.sidetone;
            sidetone_set();
            timer_of_the_shame.Enabled = false;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void notify_percentage()
        { 
            if(voltage == 0)
            {
                notifyIcon1.Icon = G733_Dolby_Atmos_companion.Properties.Resources.icon;
                progressBar1.Value = 0;
                label10.Text = "Approx -- hours";
                label9.Text = "--%";
            }
            else
            {
                percent = (int)(3451.853 - 2.885 * voltage + 0.00078188 * voltage * voltage - 0.000000067828 * voltage * voltage * voltage);

                canvas.FillEllipse(new SolidBrush(backup2), 0, 0, 32, 32);
                canvas.DrawString(
                    percent.ToString(),
                    new Font("NewTimeRoman", 18, FontStyle.Bold),
                    new SolidBrush(Color.FromArgb(0, 0, 0)),
                    new RectangleF(-5, 2, 42, 32),
                    format
                );

                estim = (34 * percent / 100);
                progressBar1.Value = Math.Min(percent, 100);
                label9.Text = percent + "%";
                label10.Text = "Approx " + (int)estim + " hours";
                notifyIcon1.Icon = Icon.FromHandle(iconBitmap.GetHicon());
                notifyIcon1.Text = voltage + " mV / " + percent + "% / Approx " + (int)estim + " hours";
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                data = HidDevice.Read();
                process_frame(data);
                if (backgroundWorker1.CancellationPending == true) return;
            } 
        }
    }
}
