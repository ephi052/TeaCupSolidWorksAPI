using System;
using System.IO;
using System.Windows.Forms; // Required for dialogs and popup
using System.Drawing; // Required for GUI elements
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CupAdjuster
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private TextBox filePathTextBox;
        private TextBox volumeTextBox;
        private TextBox destFolderTextBox;
        private Button selectFileButton;
        private Button selectFolderButton;
        private Button runButton;

        public MainForm()
        {
            // Form Settings
            this.Text = "Cup Adjuster";
            this.Size = new System.Drawing.Size(500, 250);

            // File Path TextBox
            filePathTextBox = new TextBox
            {
                Location = new System.Drawing.Point(20, 20),
                Width = 300,
                PlaceholderText = "Select a SolidWorks file..."
            };

            // Select File Button
            selectFileButton = new Button
            {
                Text = "Select File",
                Location = new System.Drawing.Point(330, 18),
                Width = 120
            };
            selectFileButton.Click += SelectFileButton_Click;

            // Destination Folder TextBox
            destFolderTextBox = new TextBox
            {
                Location = new System.Drawing.Point(20, 60),
                Width = 300,
                PlaceholderText = "Select a destination folder..."
            };

            // Select Folder Button
            selectFolderButton = new Button
            {
                Text = "Select Folder",
                Location = new System.Drawing.Point(330, 58),
                Width = 120
            };
            selectFolderButton.Click += SelectFolderButton_Click;

            // Volume TextBox
            volumeTextBox = new TextBox
            {
                Location = new System.Drawing.Point(20, 100),
                Width = 300,
                PlaceholderText = "Enter desired volume (ml)..."
            };

            // Run Button
            runButton = new Button
            {
                Text = "Run",
                Location = new System.Drawing.Point(180, 140),
                Width = 120
            };
            runButton.Click += RunButton_Click;

            // Add Controls to Form
            this.Controls.Add(filePathTextBox);
            this.Controls.Add(selectFileButton);
            this.Controls.Add(destFolderTextBox);
            this.Controls.Add(selectFolderButton);
            this.Controls.Add(volumeTextBox);
            this.Controls.Add(runButton);
        }

        private void SelectFileButton_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select SolidWorks Part File";
                openFileDialog.Filter = "SolidWorks Part Files (*.SLDPRT)|*.SLDPRT";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePathTextBox.Text = openFileDialog.FileName;
                }
            }
        }

        private void SelectFolderButton_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = "Select Destination Folder";

                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    destFolderTextBox.Text = folderBrowserDialog.SelectedPath;
                }
            }
        }

        private void RunButton_Click(object? sender, EventArgs e)
        {
            string partPath = filePathTextBox.Text;
            string destFolderPath = destFolderTextBox.Text;
            string volumeInput = volumeTextBox.Text;

            if (string.IsNullOrWhiteSpace(partPath) || !File.Exists(partPath))
            {
                MessageBox.Show("Please select a valid SolidWorks file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(destFolderPath) || !Directory.Exists(destFolderPath))
            {
                MessageBox.Show("Please select a valid destination folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!double.TryParse(volumeInput, out double targetVolume) || targetVolume <= 0)
            {
                MessageBox.Show("Please enter a valid volume (ml).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Convert target volume to cubic millimeters
            targetVolume *= 1000;

            // Close the popup window and open the parameters table window
            this.Hide();

            // Process the SolidWorks file and show parameter table
            using (ParametersForm parametersForm = new ParametersForm(partPath, destFolderPath, targetVolume))
            {
                parametersForm.ShowDialog();
            }

            this.Close();
        }
    }

    public class ParametersForm : Form
    {
        private ListView parameterListView;

        public ParametersForm(string partPath, string destFolderPath, double targetVolume)
        {
            // Form Settings
            this.Text = "Parameters and Calculations";
            this.Size = new System.Drawing.Size(800, 600);

            // ListView Settings
            parameterListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = System.Windows.Forms.View.Details, // Specify namespace explicitly
                GridLines = true,
                FullRowSelect = true
            };

            parameterListView.Columns.Add("Parameter", 300);
            parameterListView.Columns.Add("Value", 150);
            parameterListView.Columns.Add("Logic", 300);

            this.Controls.Add(parameterListView);

            // Run the process and populate the table
            RunProcess(partPath, destFolderPath, targetVolume);
        }

        private void RunProcess(string partPath, string destFolderPath, double targetVolume)
        {
            try
            {
                var progID = Type.GetTypeFromProgID("SldWorks.Application");
                if (progID == null)
                {
                    AddRow("Error", "Failed to get type from ProgID", "");
                    return;
                }

                var swAppInstance = Activator.CreateInstance(progID);
                if (swAppInstance is not SldWorks swApp)
                {
                    AddRow("Error", "Failed to connect to SolidWorks", "");
                    return;
                }

                swApp.Visible = true;

                ModelDoc2 swModel = (ModelDoc2)swApp.OpenDoc(partPath, (int)swDocumentTypes_e.swDocPART);
                if (swModel == null)
                {
                    AddRow("Error", $"Failed to open the part file: {partPath}", "");
                    return;
                }

                // Retrieve current parameters
                double cupHeight = GetParameterValue(swModel, "D1@Sketch1");
                double innerRadius = GetParameterValue(swModel, "D2@Sketch1");
                double baseRadius = GetParameterValue(swModel, "D3@Sketch1");
                double wallThickness = GetParameterValue(swModel, "D8@Sketch1");
                double filletRadius = GetParameterValue(swModel, "D6@Sketch1");
                double handleOuterArc = GetParameterValue(swModel, "D1@Sketch3");

                AddRow("Current Cup Parameters", "", "");
                AddRow("Height (D1@Sketch1)", $"{cupHeight:F2} mm", "");
                AddRow("Inner Radius (D2@Sketch1)", $"{innerRadius:F2} mm", "");
                AddRow("Base Radius (D3@Sketch1)", $"{baseRadius:F2} mm", "");
                AddRow("Wall Thickness (D8@Sketch1)", $"{wallThickness:F2} mm", "");
                AddRow("Fillet Radius (D6@Sketch1)", $"{filletRadius:F2} mm", "");

                AddRow("Current Handle Parameters", "", "");
                AddRow("Outer Arc Radius (D1@Sketch3)", $"{handleOuterArc:F2} mm", "");

                double currentInnerVolume = Math.PI * Math.Pow(innerRadius, 2) * cupHeight;
                AddRow("Current Inner Volume", $"{currentInnerVolume / 1000:F2} ml", "");

                // Cup calculations
                double aspectRatio = cupHeight / innerRadius;
                double newInnerRadius = Math.Cbrt(targetVolume / (Math.PI * aspectRatio));
                double newCupHeight = aspectRatio * newInnerRadius;

                AddRow("Calculations for Cup Adjustments", "", "");
                AddRow("Aspect Ratio", $"{aspectRatio:F2}", "Maintains original aspect ratio.");
                AddRow("New Inner Radius", $"{newInnerRadius:F2} mm", "Calculated to match target volume.");
                AddRow("New Cup Height", $"{newCupHeight:F2} mm", "Scaled proportionally to inner radius.");

                // Handle calculations
                double handleHeight = 0.67 * newCupHeight;
                double handleOffset = 0.4 * handleHeight;
                double handleAttachmentLength = 0.6 * handleHeight;
                double newHandleArc = (handleOuterArc / cupHeight) * newCupHeight;

                AddRow("Calculations for Handle Adjustments", "", "");
                AddRow("Handle Height (D3@Sketch3)", $"{handleHeight:F2} mm", "67% of the updated cup height.");
                AddRow("Handle Offset (D2@Sketch3)", $"{handleOffset:F2} mm", "40% of the handle height.");
                AddRow("Handle Attachment Length (D5@Sketch3)", $"{handleAttachmentLength:F2} mm", "60% of the handle height.");
                AddRow("Handle Outer Arc Radius (D1@Sketch3)", $"{newHandleArc:F2} mm", "Proportionally scaled based on cup height.");

                // Save the file
                string savePath = Path.Combine(destFolderPath, $"{Path.GetFileNameWithoutExtension(partPath)}_{targetVolume / 1000:F0}ml.SLDPRT");
                bool saveStatus = swModel.SaveAs(savePath);

                AddRow("Save Status", saveStatus ? "Success" : "Failed", saveStatus ? $"Saved at {savePath}" : "");
            }
            catch (Exception ex)
            {
                AddRow("Error", ex.Message, "");
            }
        }

        private void AddRow(string parameter, string value, string logic)
        {
            parameterListView.Items.Add(new ListViewItem(new[] { parameter, value, logic }));
        }

        private double GetParameterValue(ModelDoc2 model, string paramName)
        {
            Dimension dim = (Dimension)model.Parameter(paramName);
            return dim?.SystemValue * 1000.0 ?? 0; // Convert meters to mm
        }
    }
}
