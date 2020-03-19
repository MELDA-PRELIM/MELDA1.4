using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsFormsApplication5
{
    public partial class Form1 : Form
    {
        bool recording = false; //flag value to check if recording is happening.
        bool right = true; //flag to accumulate left or accumulate right
        Form2 frm2 = new Form2();
        byte[] clip_diff_array = new byte[640 * 480];
        byte[] clip_sum_array = new byte[640 * 480];
        Bitmap diff_bitmap = new Bitmap(640, 480, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
        Bitmap sum_bitmap = new Bitmap(640, 480, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
        bool bitmaps_changed = false;
        bool trigger_save_to_disc = false;

        const int camResolution_0 = 480; //number of rows
        const int camResolution_1 = 640; //number of columns
        const uint k_triggerMode = 0x830; //location of the trigger register.

        string folderName = @"C:\Users\Brian\My Documents\CamData";

        int[] diff_array = new int[camResolution_0 * camResolution_1];
        int[] sum_array = new int[camResolution_0 * camResolution_1];
        
        string binary_files_path_string = null;

        long write_out_time = 0;
        short number_of_integrations = 0;
        long recording_duration = 0;
        long time_limit = 0;

        uint trigger_type = 0; //0 = none; 1 = software; 2 = hardware (GPIO 0)
        const uint triggerUp = 0xC3000000;
        const uint triggerDown = 0xC2000000;
        uint camera_polarity = 0;

        System.Diagnostics.Stopwatch process_timer = new System.Diagnostics.Stopwatch();

        BackgroundWorker bw = new BackgroundWorker();

               
        public Form1()
        {
            InitializeComponent();
            frm2.Show();
            frm2.setText("This is a title");

            outputBox.AppendText("Number of cameras connected is:" + Convert.ToString(flyCap1.GetNumOfCameras()) + "\n");
            folderBox.Text = folderName;

            var diff_pal = diff_bitmap.Palette;
            var sum_pal = sum_bitmap.Palette;

            for (int i = 0; i < 256; i++)
            {
                diff_pal.Entries[i] = System.Drawing.Color.FromArgb(255, i, i, i);
                sum_pal.Entries[i] = System.Drawing.Color.FromArgb(255, i, i, i);
            }//This code sets the palette for each bitmap accordingly.

            diff_bitmap.Palette = diff_pal;
            sum_bitmap.Palette = sum_pal;


            if (flyCap1.GetNumOfCameras() > 0)
            {
                try
                {
                    object cameralist = flyCap1.GetCameraList();
                    object[] cameralist_array = (object[])cameralist;
                    foreach (object element in cameralist_array)
                    {
                        outputBox.AppendText("Camera list: " + element.ToString() + "\n");
                    }

                    flyCap1.Camera = 0;

                    ActiveFlyCapLib.CameraInfo caminfo = flyCap1.GetCameraInfo();
                    outputBox.AppendText("Camera serial number: " + caminfo.serialNumber.ToString() + "\n");
                    outputBox.AppendText("Camera sensor resolution: " + caminfo.sensorResolution + "\n");

                    flyCap1.SetGrabMode(ActiveFlyCapLib.GrabMode.FreeRunning);
                    outputBox.AppendText("Grab mode set to: " + flyCap1.GetGrabMode().ToString() + "\n");
                    flyCap1.videoMode = ActiveFlyCapLib.DCAMVideoMode.VideoMode_640_480_Y8;
                    outputBox.AppendText("Video mode set to: " + flyCap1.videoMode.ToString() + "\n");
                    flyCap1.AutoResize = 1; //resizes image to fit the control.
                    if (flyCap1.AutoResize == 1)
                    {
                        outputBox.AppendText("Autoresize enabled \n");
                    }
                    else
                    {
                        outputBox.AppendText("Autoresize disabled \n");
                    }
                    
                }
                catch
                {
                    outputBox.AppendText("Problem setting grab mode or video mode \n");
                }

                try
                {
                    ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                    camprop.present = 1;
                    camprop.absControl = 1;
                    camprop.onePush = 0;
                    camprop.onOff = 1;
                    camprop.autoManualMode = 0;
                    camprop.absValue = (float)setFPSUpDown.Value;
                    flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.FrameRate, ref camprop);
                    outputBox.AppendText("Camera frame rate set to " + setFPSUpDown.Value.ToString() + " Hz\n");

                }
                catch
                {
                    MessageBox.Show("Error setting camera FPS");
                }

                try
                {
                    ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                    camprop.present = 1;
                    camprop.onOff = 1;
                    camprop.absControl = 1;
                    camprop.onePush = 0;
                    camprop.autoManualMode = 0;
                    camprop.absValue = (float)shutterUpDown.Value;
                    flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Shutter, ref camprop);
                    outputBox.AppendText("Camera shutter set to " + shutterUpDown.Value.ToString() + " ms\n");
                }
                catch
                {
                    MessageBox.Show("Error setting camera shutter");
                }

                try
                {
                    ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                    camprop.present = 1;
                    camprop.onOff = 1;
                    camprop.absControl = 1; //control with value in abs value CSR. the value in the Value field is read-only.
                    camprop.onePush = 0;
                    camprop.autoManualMode = 0;
                    camprop.absValue = (float)gainNumericUpDown.Value;
                    flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gain, ref camprop);
                    outputBox.AppendText("Camera gain set to " + gainNumericUpDown.Value.ToString() + " dB\n");
                }
                catch
                {
                    MessageBox.Show("Error setting gain");
                }

                double trackbarpercentage = ((float)gainNumericUpDown.Value + 2.81) / (24.00 + 2.81) * 100.00; //min -2.81, max 24
                gainTrackBar.Value = (int)trackbarpercentage; //updates the gain trackbar percentage approximately

                try
                {
                    ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                    camprop.present = 1;
                    camprop.onOff = 1;
                    camprop.absControl = 1; //control with value in abs value CSR. the value in the Value field is read-only.
                    camprop.onePush = 0;
                    camprop.autoManualMode = 0;
                    camprop.absValue = (float)gammaNumericUpDown.Value;
                    flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gamma, ref camprop);
                    outputBox.AppendText("Camera gamma set to " + gammaNumericUpDown.Value.ToString() + "\n");
                }
                catch
                {
                    MessageBox.Show("Error setting gamma");
                }

                trackbarpercentage = ((float)gammaNumericUpDown.Value - 0.50) / (4.00 - 0.50) * 100.00; //min 0.5, max 4
                gammaTrackBar.Value = (int)trackbarpercentage; //updates the gamma trackbar percentage approximately

                try
                {
                    ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                    camprop.present = 1;
                    camprop.onOff = 1;
                    camprop.absControl = 0; //no absolute value for sharpness
                    camprop.onePush = 0;
                    camprop.autoManualMode = 0;
                    camprop.valueA = (uint)sharpnessNumericUpDown.Value;
                    flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Sharpness, ref camprop);
                    outputBox.AppendText("Camera sharpness set to " + sharpnessNumericUpDown.Value.ToString() + "\n");
                }
                catch
                {
                    MessageBox.Show("Error setting sharpness");
                }

                trackbarpercentage = ((float)sharpnessNumericUpDown.Value - 0.00) / (4095.00 - 0.00) * 100.00; //min 0, max 4095
                sharpnessTrackBar.Value = (int)trackbarpercentage; //updates the sharpness trackbar percentage approximately

                bw.WorkerSupportsCancellation = true;
                bw.WorkerReportsProgress = true;

                //add the event handlers to the Backgroundworker's instance events
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);
                bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);

                flyCap1.Start = 1;


            }
            


        }

        private void startButton_Click(object sender, EventArgs e)
        {
            try
            {
                flyCap1.Start = 0;
                flyCap1.SetGrabMode(ActiveFlyCapLib.GrabMode.FreeRunning);

                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 0;
                camprop.absControl = 1;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.AutoExposure, ref camprop); //no auto exposure
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Brightness, ref camprop); //no brightness

                //camprop.onOff = 1;
                //camprop.onePush = 1;
                
                //flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gain, ref camprop); //onepush gain
                //flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gamma, ref camprop); //onepush gamma
                //flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Hue, ref camprop); //onepush hue
                //flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Iris, ref camprop); //onepush iris
                //flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Saturation, ref camprop); //onepush saturation
                //flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.WhiteBalance, ref camprop); //onepush whitebalance

                flyCap1.Start = 1; //start grabbing images. this will not draw any image to screen.
                flyCap1.Display = 1; //draws all incoming images onto the control.

            }
            catch
            {
                MessageBox.Show("Some error has occurred in starting the camera, displaying the video, allowing it to resize, and/or getting the frame rate");
            }



        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            if (recording)
            {
                bw.CancelAsync();
                recording = false;
            }

            else
            {
                try
                {
                    flyCap1.Start = 0; //this will stop the camera, ending isochronous data transmission.

                }
                catch
                {
                    MessageBox.Show("Some error has occurred in stopping the camera");
                }
            }
        }

        private void recButton_Click(object sender, EventArgs e)
        {
            if (intervalUpDown.Value >= 0)
            {
                write_out_time = (long)intervalUpDown.Value; //integration interval.
            }
            else
            {
                outputBox.AppendText("Please choose a positive integration Time.\n");
            }

            if (recdurationUpDown.Value > 0)
            {
                recording_duration = (long)recdurationUpDown.Value; //recording duration
            }
            else
            {
                outputBox.AppendText("Please choose a positive, non-zero Recording Time.\n");
            }

            //File storage details.
            string pathString = Path.Combine(folderName, Convert.ToString(trialUpDown.Value));

            //Creates new directory so that images can be stored.
            bool directory_created = false;

            if (!Directory.Exists(pathString))
            {
                Directory.CreateDirectory(pathString); //create the storage directory if it doesn't exist. 
                directory_created = true;
                binary_files_path_string = pathString;
            }
            else if (recording)
            {
                outputBox.AppendText("Recording in progress. Press stop button to stop recording.\n");
            }
            else
            {
                outputBox.AppendText("Please Increment Trial Number \n");
            }


            if (directory_created && write_out_time >= 0 && recording_duration > 0)
            {
                flyCap1.Start = 0;
                recLabel.Text = "Rec. On";
                recording = true;

                Array.Clear(diff_array, 0, diff_array.Length); //zeros out array.
                Array.Clear(sum_array, 0, sum_array.Length);
                number_of_integrations = 0;
                trigger_save_to_disc = false;

                //ensure that the input boxes don't get changed during recording.
                intervalUpDown.IsAccessible = false;
                trialUpDown.IsAccessible = false;
                shutterUpDown.IsAccessible = false;
                recdurationUpDown.IsAccessible = false;
                folderBox.IsAccessible = false;
                folderButton.IsAccessible = false;
                triggerComboBox.IsAccessible = false;
                             
                List<object> arguments = new List<object>();
                arguments.Add(flyCap1);
                arguments.Add(pathString);
                arguments.Add((float)setFPSUpDown.Value);

                outputBox.AppendText("Handing off to background worker...");

                bw.RunWorkerAsync(arguments);

            }
                
        }

       
        private void setFPSUpDown_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 1;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.absValue = (float)setFPSUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.FrameRate, ref camprop);
                outputBox.AppendText("Camera frame rate set to " + Convert.ToString(setFPSUpDown.Value) + " FPS\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera FPS");
            }
        }


        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            List<object> genericlist = e.Argument as List<object>;

            AxActiveFlyCapLib.AxActiveFlyCapControl flycap2 = genericlist[0] as AxActiveFlyCapLib.AxActiveFlyCapControl;

            String pathString2 = genericlist[1] as String;

            float FPSvalue = (float)genericlist[2];

            flycap2.Start = 0;
            ActiveFlyCapLib.TriggerStruct my_triggers = new ActiveFlyCapLib.TriggerStruct();

            if (trigger_type == 0)
            {
                my_triggers.isOnOff = 0;
                outputBox.AppendText("No triggering selected.\n");
            }

            else if (trigger_type == 1)
            {
                my_triggers.isOnOff = 1;
                my_triggers.source = 7; //For software triggering
                my_triggers.mode = 0; //This is for classical triggering.
                outputBox.AppendText("Software triggering selected.\n");
                flycap2.SetTrigger(ref my_triggers);
            }

            else if (trigger_type == 2)
            {   
                //use registers to set this
                const uint k_triggerVal = 0xC2000000; //presence_inq = 1, abs_control = 1, on_off = 1, trigger_polarity = 0, trigger_source = 0, trigger_mode = 0

                flycap2.WriteRegister(k_triggerMode, k_triggerVal);

                outputBox.AppendText("Hardware triggering selected.\n");
            }


            ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
            camprop.present = 1;
            camprop.absControl = 1;
            camprop.onePush = 0;
            camprop.onOff = 1;
            camprop.autoManualMode = 0;
            camprop.absValue = FPSvalue;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.FrameRate, ref camprop);

            camprop.present = 1;
            camprop.absControl = 1;
            camprop.onePush = 0;
            camprop.onOff = 1;
            camprop.autoManualMode = 0;
            camprop.absValue = (float) shutterUpDown.Value; //1ms exposure time should be ok?
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.Shutter, ref camprop);

            
            /*float brightness_val = flycap2.GetProperty(ActiveFlyCapLib.CameraPropertyType.Brightness).absValue;
            float gain_val = flycap2.GetProperty(ActiveFlyCapLib.CameraPropertyType.Gain).absValue;
            float gamma_val = flycap2.GetProperty(ActiveFlyCapLib.CameraPropertyType.Gamma).absValue;
            float hue_val = flycap2.GetProperty(ActiveFlyCapLib.CameraPropertyType.Hue).absValue;
            float iris_val = flycap2.GetProperty(ActiveFlyCapLib.CameraPropertyType.Iris).absValue;
            float saturation_val = flycap2.GetProperty(ActiveFlyCapLib.CameraPropertyType.Saturation).absValue;
            float whitebalance_val = flycap2.GetProperty(ActiveFlyCapLib.CameraPropertyType.WhiteBalance).absValue;

            camprop.absValue = brightness_val;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.Brightness, ref camprop);

            camprop.absValue = gain_val;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gain, ref camprop);

            camprop.absValue = gamma_val;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gamma, ref camprop);

            camprop.absValue = hue_val;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.Hue, ref camprop);

            camprop.absValue = iris_val;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.Iris, ref camprop);

            camprop.absValue = saturation_val;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.Saturation, ref camprop);

            camprop.absValue = whitebalance_val;
            flycap2.SetProperty(ActiveFlyCapLib.CameraPropertyType.WhiteBalance, ref camprop);*/


            flycap2.SetGrabMode(ActiveFlyCapLib.GrabMode.LockNext);
            flycap2.Start = 1;
            flycap2.Display = 0;

            flycap2.ImageGrabbed += new EventHandler(flycap2_ImageGrabbed);

            time_limit = recording_duration; 

            process_timer.Reset();
            process_timer.Start();

            while (process_timer.ElapsedMilliseconds <= time_limit || process_timer.ElapsedMilliseconds <= write_out_time)
            {
                if (worker.CancellationPending)
                {
                    flycap2.Start = 0;
                    e.Cancel = true;
                    break;
                }

                else
                {
                    
                    flycap2.GrabImage();

                    /*if (process_timer.ElapsedMilliseconds <= time_limit)
                    {
                        worker.ReportProgress((int)(process_timer.ElapsedMilliseconds / (float)time_limit * 100));
                    }*/

                    if (trigger_save_to_disc)
                    {
                        save_to_disc();
                        trigger_save_to_disc = false;
                    }

                    if (bitmaps_changed)
                    {
                        List<object> my_bitmaps = new List<object>();
                        my_bitmaps.Add(diff_bitmap);
                        my_bitmaps.Add(sum_bitmap);
                        worker.ReportProgress((int)(process_timer.ElapsedMilliseconds / (float)time_limit * 100), my_bitmaps);
                        bitmaps_changed = false;
                    }
                }
            }//end while

            if (!(Array.TrueForAll(diff_array, isZero)) || !(Array.TrueForAll(sum_array, isZero)))
            {
                write_out_time = 0; //we want ImageGrabbed event to write to disc the moment an even number_of_integrations is obtained.
                //We set write_out_time to zero so process_timer.ElapsedMilliseconds will always be greater than it.

                while (number_of_integrations % 2 != 0)
                {
                    flycap2.GrabImage();
                }
            }

            flycap2.Start = 0;

        } //end bw_DoWork

        private void flycap2_ImageGrabbed(object sender, EventArgs e)
        {
            //NOTE: ASSUME HARDWARE TRIGGERING HERE (probably should check)

            camera_polarity = flyCap1.GetTrigger().polarity;

            if (camera_polarity == 0)
            {
                flyCap1.WriteRegister(k_triggerMode, triggerUp);
            }
            else
            {
                flyCap1.WriteRegister(k_triggerMode, triggerDown);
            }

            camera_polarity = flyCap1.GetTrigger().polarity;

            if (right)
            {
                unsafe
                {
                    IntPtr inpoint = new IntPtr((UInt32)flyCap1.GetImagePtr(ActiveFlyCapLib.ImageType.RawImage));
                    byte* rawimgptr = (byte*)inpoint.ToPointer();
                    for (int i = 0; i < diff_array.Length; i++, rawimgptr++)
                    {
                        diff_array[i] -= *rawimgptr;
                        sum_array[i] += *rawimgptr;
                    }

                }//end unsafe

                right = false;
                number_of_integrations += 1;

                //write this information to log file
                using (StreamWriter sw = File.AppendText(Path.Combine(binary_files_path_string, "logfile.csv")))
                {
                    sw.WriteLine(process_timer.ElapsedMilliseconds.ToString() + "," + "right integrate");
                }          
            }

            else
            {
                unsafe
                {
                    IntPtr inpoint = new IntPtr((UInt32)flyCap1.GetImagePtr(ActiveFlyCapLib.ImageType.RawImage));
                    byte* rawimgptr = (byte*)inpoint.ToPointer();
                    for (int i = 0; i < diff_array.Length; i++, rawimgptr++)
                    {
                        diff_array[i] += *rawimgptr;
                        sum_array[i] += *rawimgptr;
                    }

                }//end unsafe

                right = true;
                number_of_integrations += 1;

                //write this information to log file
                using (StreamWriter sw = File.AppendText(Path.Combine(binary_files_path_string, "logfile.csv")))
                {
                    sw.WriteLine(process_timer.ElapsedMilliseconds.ToString() + "," + "left integrate");
                }
            }

            //Decide if it is time to write out to disc, update the progress bar and update the images shown in GUI on screen.
            if (process_timer.ElapsedMilliseconds >= write_out_time && number_of_integrations % 2 == 0)
            {
                if (write_out_time < time_limit)
                {
                    write_out_time = process_timer.ElapsedMilliseconds + (long)intervalUpDown.Value;
                }

                trigger_save_to_disc = true;
                outputBox.AppendText("Save to disc triggered\n");
                
            }
            
            
        }


        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            recBar.Value = e.ProgressPercentage;
            List<object> bitmap_list = e.UserState as List<object>;
            updateForm2(bitmap_list[0] as Bitmap, bitmap_list[1] as Bitmap);
            bitmap_list.Clear(); //is this necessary to do?
            flyCap1.DrawSingleImage(0); //this works. draws to screen once per integration interval.

            //can we just refer to diff_bitmap and sum_bitmap here without passing around a list of objects?
        } 

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                outputBox.AppendText(e.Error.Message + "\n");
                outputBox.AppendText("Recording Error! \n");
                
            }

            else if (e.Cancelled)
            {
                outputBox.AppendText("Recording cancelled! \n");
            }

            else
            {
                outputBox.AppendText("Recording finished! \n");
            }
                recLabel.Text = "Rec. Off";
                recording = false;
                intervalUpDown.IsAccessible = true;
                trialUpDown.IsAccessible = true;
                shutterUpDown.IsAccessible = true;
                recdurationUpDown.IsAccessible = true;
                folderBox.IsAccessible = true;
                folderButton.IsAccessible = true;
                triggerComboBox.IsAccessible = true;
                flyCap1.Start = 0;
                flyCap1.ImageGrabbed -= flycap2_ImageGrabbed;    
                process_timer.Stop();

                ActiveFlyCapLib.TriggerStruct my_triggers = new ActiveFlyCapLib.TriggerStruct();
                my_triggers.isOnOff = 0;
                flyCap1.SetTrigger(ref my_triggers);
            

        }

        private void Trigger_button_Click(object sender, EventArgs e)
        {
            //This will initiate a burst of software triggers at the frequency indicated by TriggerFreqUpDown

            
            long trigger_sequence_length = (long) triggerLenUpDown.Value;

            BackgroundWorker bt = new BackgroundWorker();
            bt.DoWork += delegate
            {

                Trigger_button.IsAccessible = false;
                TriggerFreqUpDown.IsAccessible = false;
                triggerLenUpDown.IsAccessible = false;
                Trigger_button.IsAccessible = false;
                System.Diagnostics.Stopwatch trigger_timer = new System.Diagnostics.Stopwatch();
                long trigger_duration = trigger_sequence_length; //Could be longer, could be shorter.

                long fire_a_trigger = 1000 / (long)TriggerFreqUpDown.Value; //if this is 5Hz, then it will trigger every 1000/5 = 200ms.

                trigger_timer.Start();

                while (trigger_timer.ElapsedMilliseconds <= trigger_duration)
                {
                    if (trigger_timer.ElapsedMilliseconds >= fire_a_trigger)
                    {
                        flyCap1.WriteRegister(0x62C, 0x80000000);
                        outputBox.AppendText("Trigger fired at " + process_timer.ElapsedMilliseconds.ToString() + " ms.\n");
                        fire_a_trigger += 1000 / (long)TriggerFreqUpDown.Value;
                    }
                }

                trigger_timer.Stop();
                
            };



            bt.RunWorkerCompleted += delegate
            {
                triggerLenUpDown.IsAccessible = true;
                Trigger_button.IsAccessible = true;
                Trigger_button.IsAccessible = true;
                TriggerFreqUpDown.IsAccessible = true;
            };

            bt.RunWorkerAsync();

        }

        private void shutterUpDown_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 1;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.absValue = (float)shutterUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Shutter, ref camprop);
                outputBox.AppendText("Camera shutter set to " + Convert.ToString(shutterUpDown.Value) + " ms\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera shutter");
            }
        }

        private void folderButton_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                folderBox.Text = folderBrowserDialog1.SelectedPath;
            }

            folderName = folderBox.Text;
        }

        private void folderBox_TextChanged(object sender, EventArgs e)
        {
            folderName = folderBox.Text;
        }

        private void triggerComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(triggerComboBox.Text.Equals("Software", StringComparison.OrdinalIgnoreCase))
            {
                trigger_type = 1;
                outputBox.AppendText("Software trigger.\n");
            }

            else if(triggerComboBox.Text.Equals("Hardware (GPIO 0)", StringComparison.OrdinalIgnoreCase))
            {
                trigger_type = 2;
                outputBox.AppendText("Hardware trigger.\n");
            }

            else if (triggerComboBox.Text.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                trigger_type = 0;
                outputBox.AppendText("No trigger.\n");
            }

        }

        private static bool isZero(int input)
        {
            if (input.Equals(0))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static byte Clip(decimal input)
        {
            if (input > 255)
            {
                return 255;
            }

            else if (input < 0)
            {
                return 0;
            }

            else
            {
                return (byte)input;
            }
        }

        private void save_to_disc()
        {
            string save_to_disc_time = process_timer.ElapsedMilliseconds.ToString();
            string file_name_bin = save_to_disc_time + ".bin";

            decimal diff_input = 0;
            decimal sum_input = 0;
            outputBox.AppendText("Number of integrations = " + number_of_integrations.ToString() + " \n");

            using(FileStream fs = new FileStream(Path.Combine(binary_files_path_string, "diff_array_" + file_name_bin), FileMode.OpenOrCreate, FileAccess.Write))
            {
                using(BinaryWriter bw = new BinaryWriter(fs))
                {
                    //foreach(short value in diff_array)
                    decimal offset_value = Math.Round(OffsetUpDown.Value / 100 * 255);
                    for(int i = 0; i < diff_array.Length; i++)
                    {
                        bw.Write((short)(diff_array[i] / write_out_time * 1000)); //Divide entries by number of seconds taken to accumulate data because data/sec will always fit in 16 bit signed integer
                        diff_input = (decimal)(2 * diff_array[i] / number_of_integrations) + offset_value;
                        clip_diff_array[i] = Clip(diff_input);
                    }
                }
            }

            using (FileStream fs = new FileStream(Path.Combine(binary_files_path_string, "sum_array_" + file_name_bin), FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    //foreach (short value in sum_array)
                    for(int i=0; i < sum_array.Length; i++)
                    {
                        bw.Write((short)(sum_array[i]/write_out_time*1000)); //Divide entries by number of seconds taken to accumulate data because data/sec will always fit in 16 bit signed integer
                        sum_input = (decimal)(sum_array[i] / number_of_integrations); //note, divisor is twice that of diff_input to prevent overscale
                        clip_sum_array[i] = Clip(sum_input);
                    }
                }
            }
            
            outputBox.AppendText("Files written to disc at " + save_to_disc_time + " ms.\n");

            //write this information to log file
            using (StreamWriter sw = File.AppendText(Path.Combine(binary_files_path_string, "logfile.csv")))
            {
                sw.WriteLine(save_to_disc_time + "," + "write out to disc");
            }

            //This part of the code works on diff_bitmap and sum_bitmap, which are global variables.
            //diff_bitmap.Dispose();
            //sum_bitmap.Dispose(); //are these dispose calls necessary?
            //var local_diff_bitmap = new Bitmap(640, 480, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            //var local_sum_bitmap = new Bitmap(640, 480, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            /*var diff_pal = diff_bitmap.Palette;
            var sum_pal = sum_bitmap.Palette;

            for(int i = 0; i < 256; i++)
            {
                diff_pal.Entries[i] = System.Drawing.Color.FromArgb(255, i, i);
                sum_pal.Entries[i] = System.Drawing.Color.FromArgb(255, i, i);
            }//This code sets the palette for each bitmap accordingly.

            diff_bitmap.Palette = diff_pal;
            sum_bitmap.Palette = sum_pal;*/

            var diff_bitmap_data = diff_bitmap.LockBits(new Rectangle(0, 0, 640, 480), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            System.Runtime.InteropServices.Marshal.Copy(clip_diff_array, 0, diff_bitmap_data.Scan0, 640 * 480);
            diff_bitmap.UnlockBits(diff_bitmap_data);
            //diff_bitmap = local_diff_bitmap;
            //local_diff_bitmap.Dispose();

            var sum_bitmap_data = sum_bitmap.LockBits(new Rectangle(0, 0, 640, 480), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            System.Runtime.InteropServices.Marshal.Copy(clip_sum_array, 0, sum_bitmap_data.Scan0, 640 * 480);
            sum_bitmap.UnlockBits(sum_bitmap_data);
            //sum_bitmap = local_sum_bitmap;
            //local_sum_bitmap.Dispose();

            bitmaps_changed = true;
            outputBox.AppendText("bitmaps_changed: " + bitmaps_changed.ToString() + "\n");

            Array.Clear(diff_array, 0, diff_array.Length); //zeros out array.
            Array.Clear(sum_array, 0, sum_array.Length);
            number_of_integrations = 0;

        }

        private void updateForm2(Bitmap diff_bitmap, Bitmap sum_bitmap)
        {
            frm2.setLeftImage(diff_bitmap);
            frm2.setRightImage(sum_bitmap);
            frm2.setText("This is changing");
        }


        private void gainNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 1;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.absValue = (float)gainNumericUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gain, ref camprop);
                outputBox.AppendText("Camera gain set to " + Convert.ToString(gainNumericUpDown.Value) + " dB\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera gain");
            }

            double trackbarpercentage = ((float)gainNumericUpDown.Value + 2.81) / (24.00 + 2.81) * 100.00; //min -2.81, max 24
            gainTrackBar.Value = (int)trackbarpercentage; //updates the gain trackbar percentage approximately
        }

        private void gammaNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 1;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.absValue = (float)gammaNumericUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gamma, ref camprop);
                outputBox.AppendText("Camera gamma set to " + Convert.ToString(gammaNumericUpDown.Value) + "\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera gamma");
            }

            double trackbarpercentage = ((float)gammaNumericUpDown.Value - 0.50) / (4.00 - 0.50) * 100.00; //min 0.5, max 4
            gammaTrackBar.Value = (int)trackbarpercentage; //updates the gamma trackbar percentage approximately
        }

        private void sharpnessNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 0;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.valueA = (uint)sharpnessNumericUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Sharpness, ref camprop);
                outputBox.AppendText("Camera sharpness set to " + Convert.ToString(sharpnessNumericUpDown.Value) + "\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera sharpness");
            }

            double trackbarpercentage = ((float)sharpnessNumericUpDown.Value - 0.00) / (4095.00 - 0.00) * 100.00; //min 0, max 4095
            sharpnessTrackBar.Value = (int)trackbarpercentage; //updates the sharpness trackbar percentage approximately
        }

        private void gainTrackBar_Scroll(object sender, EventArgs e)
        {
            int trackbarpercentage = gainTrackBar.Value;
            double gainvalue = (double)trackbarpercentage / 100.00 * (24.00 + 2.81) - 2.81;
            gainNumericUpDown.Value = (decimal)gainvalue;

            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 1;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.absValue = (float)gainNumericUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gain, ref camprop);
                outputBox.AppendText("Camera gain set to " + Convert.ToString(gainNumericUpDown.Value) + " dB\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera gain");
            }
        }

        private void gammaTrackBar_Scroll(object sender, EventArgs e)
        {
            int trackbarpercentage = gammaTrackBar.Value;
            double gainvalue = (double)trackbarpercentage / 100.00 * (4.00 - 0.50) + 0.50;
            gammaNumericUpDown.Value = (decimal)gainvalue;

            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 1;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.absValue = (float)gammaNumericUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Gamma, ref camprop);
                outputBox.AppendText("Camera gamma set to " + Convert.ToString(gammaNumericUpDown.Value) + "\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera gamma");
            }
        }

        private void sharpnessTrackBar_Scroll(object sender, EventArgs e)
        {
            int trackbarpercentage = sharpnessTrackBar.Value;
            double gainvalue = (double)trackbarpercentage / 100.00 * (4095.00 - 0.00) + 0.00;
            sharpnessNumericUpDown.Value = (decimal)gainvalue;

            try
            {
                flyCap1.Start = 0;
                ActiveFlyCapLib.CameraProperty camprop = new ActiveFlyCapLib.CameraProperty();
                camprop.present = 1;
                camprop.onOff = 1;
                camprop.absControl = 0;
                camprop.onePush = 0;
                camprop.autoManualMode = 0;
                camprop.valueA = (uint)sharpnessNumericUpDown.Value;
                flyCap1.SetProperty(ActiveFlyCapLib.CameraPropertyType.Sharpness, ref camprop);
                outputBox.AppendText("Camera sharpness set to " + Convert.ToString(sharpnessNumericUpDown.Value) + "\n");
                flyCap1.Start = 1;

            }
            catch
            {
                MessageBox.Show("Error setting camera sharpness");
            }
        }
 


    }
}
