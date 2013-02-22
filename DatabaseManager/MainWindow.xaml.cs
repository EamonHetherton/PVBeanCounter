using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PVSettings;
using MackayFisher.Utilities;
using DatabaseUtilities;

namespace DatabaseUtilities
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ApplicationSettings ApplicationSettings;
        String StructureFileName;
        DBStructure Structure;
        SchemaStructure Schema;
        SystemServices SystemServices;
        DatabaseContext TargetDb;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationSettings = new PVSettings.ApplicationSettings("settings.xml", null);
            StructureFileName = AppDomain.CurrentDomain.BaseDirectory + @"\PVHistory.dbschema";

            SystemServices = new MackayFisher.Utilities.SystemServices(ApplicationSettings.BuildFileName("DatabaseManager.log"));
            TargetDb = new DatabaseContext();

            tabSourceDB.DataContext = ApplicationSettings;
            tabTargetDB.DataContext = TargetDb;

            Structure = new DBStructure(StructureFileName);
            Schema = new SchemaStructure("pvhistory", Structure.StructureDoc);

            //Database db = new Database(ApplicationSettings, SystemServices, ApplicationSettings.DefaultDirectory);


        }
    }
}
