using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Net;
using System.IO;

namespace APRA_parser
{
    public partial class Form1 : Form
    {
        System.Timers.Timer timerProgresivo = new System.Timers.Timer(25000);
        System.Timers.Timer timerEnvio = new System.Timers.Timer();
        String lastPush = "";
        String file = "config.ini";
        String head = "[BOF]\n[BOH]\nASCIICode.Separator = 44\nASCIICode.Decimal = 46\nMissingValues = \"\"\nResolution=Min.01\nIntegrationPeriod = Forward\nDateFormat = YYYYMMDD\nTimeFormat = HH:NN\nStation.Name = \"Centenario\"\nStation.Parameters.Count = 12\nStation.Parameters(1).Name =\"NO;Roof;Conc\"\nStation.Parameters(1).Unit =\"ppb\"\nStation.Parameters(1).Position = 1\nStation.Parameters(2).Name =\"NO2;Roof;Conc\"\nStation.Parameters(2).Unit =\"ppb\"\nStation.Parameters(2).Position = 2\nStation.Parameters(3).Name =\"NOx;Roof;Conc\"\nStation.Parameters(3).Unit =\"ppb\"\nStation.Parameters(3).Position = 3\nStation.Parameters(4).Name =\"CO;Roof;Conc\"\nStation.Parameters(4).Unit =\"ppm\"\nStation.Parameters(4).Position = 4\nStation.Parameters(5).Name =\"PM10;Roof;Conc\"\nStation.Parameters(5).Unit =\"ug/m3\"\nStation.Parameters(5).Position = 5\nStation.Parameters(6).Name =\"Wind Direction;6 m;Value\"\nStation.Parameters(6).Unit =\"Degrees\"\nStation.Parameters(6).Position = 6\nStation.Parameters(7).Name =\"Wind Speed;Mast 6m;Value\"\nStation.Parameters(7).Unit =\"m/s\"\nStation.Parameters(7).Position = 7\nStation.Parameters(8).Name =\"Temperature;Mast 2m;Value\"\nStation.Parameters(8).Unit =\"Celsius\"\nStation.Parameters(8).Position = 8\nStation.Parameters(9).Name =\"Rel Humidity;Mast 2m;Value\"\nStation.Parameters(9).Unit =\"%\"\nStation.Parameters(9).Position = 9\nStation.Parameters(10).Name =\"Atm Pressure;Roof;Value\"\nStation.Parameters(10).Unit =\"hPa\"\nStation.Parameters(10).Position = 10\nStation.Parameters(11).Name =\"Global Radiation;Roof;Value\"\nStation.Parameters(11).Unit =\"W/m2\"\nStation.Parameters(11).Position = 11\nStation.Parameters(12).Name =\"Rain;Roof;Value\"\nStation.Parameters(12).Unit =\"mm\"\nStation.Parameters(12).Position = 12\n[EOH]\n[BOD]\n";
        String data = "";
        bool estado = false;
        String carpeta = "";
        bool scroll = true;
        String url = "";
        String configs = "";
        DateTime last;
        String info;
        int global = 0;
        String[] sensores = { "atmpressure", "carbonoxide", "globalradiation", "mononitrogenoxide", "nitricdioxide", "nitricoxide", "particulatematter", "rain", "relativehumidity", "temperature", "winddirection", "windspeed" };

        private void pushData(Object source, System.Timers.ElapsedEventArgs e)
        {
            global++;
            var listaArch = Directory.GetFiles(carpeta);
            foreach(String archivo in listaArch)
            {
                String archivoFin = archivo.Substring(carpeta.Length+1);
                if (archivoFin == String.Format("PromedioMin{0}.Calc", last.Month.ToString()))
                {
                    addLog(String.Format("Archivo {0} existente", archivoFin));
                    String line;
                    StreamReader reader = new StreamReader(File.Open(archivo,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite));
                    int cont = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lastPush = last.ToString("dd/MM/yy,HH:mm");
                        if (line.IndexOf(lastPush) != -1)
                        {
                            cont++;
                            DateTime lastPushDate = DateTime.Parse(lastPush);
                            info = lastPushDate.ToString("yyyyMMdd,HH:mm") + line.Substring(lastPush.Length)+"\n";
                            addLog(lastPush + ": Nueva información encontrada: ");
                            addLog(info);
                            data += info;
                            estado = true;
                            last = last.AddMinutes(1.00);
                            global = 0;
                        }
                    }
                    if (cont == 0 && global>3)
                    {
                        if (last < DateTime.Now)
                            last = last.AddMinutes(1.00);
                        else
                            last = DateTime.Now;
                    }
                    reader.Close();
                }
            }
        }
        private void addLog(String text)
        {
            if (InvokeRequired)
            {
                richTextBox1.Invoke((Action) delegate
                {
                    richTextBox1.AppendText(text + "\n");
                    if(scroll)
                        richTextBox1.ScrollToCaret();
                });
            }
            else
            {
                richTextBox1.AppendText(text + "\n");
                if (scroll)
                    richTextBox1.ScrollToCaret();
            }
        }
        private int sendData(String datos)
        {
            try
            {
                var baseUri = new Uri(url);
                WebClient client = new WebClient();
                client.Headers.Add("Content-Type", "text/plain");
                datos = head + datos + "[EOD]\n[EOF]";
                addLog(datos);
                var response = client.UploadString(baseUri, datos);
                addLog("Mandando a la API");
                return 202;
            }
            catch (WebException ex)
            {
                return (int)((HttpWebResponse)ex.Response).StatusCode;
            }
        }

        private void send(Object source, System.Timers.ElapsedEventArgs e)
        {
            if (estado && data != "")
            {
                int code = sendData(data);
                if (code == 200 || code == 202)
                {
                    addLog("Información enviada a la API");
                    estado = false;
                    data = "";
                }
                else
                {
                    addLog("Error pusheando a la API: " + code.ToString());
                }
            }
        }

        public Form1()
        {
            InitializeComponent();
            if (File.Exists(file))
            {
                String contenido = File.ReadAllText(file);
                String[] configurations = contenido.Split('\n');
                comboBox1.Text = configurations[0];
                textBox1.Text = configurations[1];
            }
            else
            {
                textBox1.Text = "http://bapocbulkserver.azurewebsites.net/api1/otfs/centenario";
            }
            timerEnvio.Elapsed += send;
            timerProgresivo.Elapsed += pushData;
            addLog("Programa inicializado");
            addLog("El servicio está actualmente detenido");
            dateTimePicker1.CustomFormat = "dd/MM/yyyy HH:mm";
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dial = new FolderBrowserDialog();
            DialogResult result = dial.ShowDialog();
            if (result == DialogResult.OK)
            {
                comboBox1.Text = dial.SelectedPath;
            }
            addLog("Carpeta Seleccionada: " + comboBox1.Text);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        
        private void button3_Click(object sender, EventArgs e)
        {
            if (label5.Text == "Stopped" && Directory.Exists(comboBox1.Text))
            {
                carpeta = comboBox1.Text;
                label5.Text = "Running";
                label5.ForeColor = Color.Green;
                button3.Text = "STOP";
                timerProgresivo.Start();
                timerEnvio.Interval = (int)numericUpDown1.Value * 60000;
                timerEnvio.Start();
                numericUpDown1.Enabled = false;
                comboBox1.Enabled = false;
                button1.Enabled = false;
                dateTimePicker1.Enabled = false;
                textBox1.Enabled = false;
                configs += carpeta + "\n";
                configs += textBox1.Text + "\n";
                last = dateTimePicker1.Value;
                button3.ForeColor = Color.Red;
                addLog("Servicio comenzado...");
                File.WriteAllText(file, configs);
            }
            else if (label5.Text == "Stopped" && !Directory.Exists(comboBox1.Text))
            {
                MessageBox.Show("Carpeta no seleccionada o Inexistente", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                addLog("Error en la selección de carpeta");
            }
            else
            {
                numericUpDown1.Enabled = true;
                comboBox1.Enabled = true;
                textBox1.Enabled = true;
                button1.Enabled = true;
                dateTimePicker1.Enabled = true;
                label5.Text = "Stopped";
                timerProgresivo.Stop();
                label5.ForeColor = Color.Red;
                timerEnvio.Stop();
                button3.Text = "START";
                button3.ForeColor = Color.Green;
                addLog("Servicio terminado...");
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                scroll = true;
            }
            else
            {
                scroll = false;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            url = textBox1.Text;
        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }
    }
}
