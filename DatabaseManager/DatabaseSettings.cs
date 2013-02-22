using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using DatabaseUtilities;

namespace DatabaseUtilities
{
    public class DatabaseContext : INotifyPropertyChanged
    {
        private DBParameters parameters;

        public DatabaseContext()
        {
            parameters.ConnectionString = "";
            parameters.DatabaseName = "";
            parameters.DatabaseType = "";
            parameters.DbDirectory = "";
            parameters.Host = "";
            parameters.OleDbName = "";
            parameters.Password = "";
            parameters.ProviderName = "";
            parameters.ProviderType = "";
            parameters.UserName = "";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        public String StandardDBType
        {
            get
            {
                for (int i = 0; i <= ApplicationSettings.StandardDBMatrix.GetUpperBound(0); i++)
                {
                    if ((DatabaseType == ApplicationSettings.StandardDBMatrix[i, 1])
                    && (ProviderType == ApplicationSettings.StandardDBMatrix[i, 2])
                    && (ProviderName == ApplicationSettings.StandardDBMatrix[i, 3])
                    && (OleDbName == ApplicationSettings.StandardDBMatrix[i, 4])
                    && (DatabaseName == ApplicationSettings.StandardDBMatrix[i, 5]))
                        return ApplicationSettings.StandardDBMatrix[i, 0];
                }

                return "Custom";
            }

            set
            {
                if (value == "Custom")
                    return;

                String prevDatabaseType = DatabaseType;

                for (int i = 0; i <= ApplicationSettings.StandardDBMatrix.GetUpperBound(0); i++)
                    if (ApplicationSettings.StandardDBMatrix[i, 0] == value)
                    {
                        DatabaseType = ApplicationSettings.StandardDBMatrix[i, 1];
                        ProviderType = ApplicationSettings.StandardDBMatrix[i, 2];
                        ProviderName = ApplicationSettings.StandardDBMatrix[i, 3];
                        OleDbName = ApplicationSettings.StandardDBMatrix[i, 4];
                        DatabaseName = ApplicationSettings.StandardDBMatrix[i, 5];
                        if (UserName == "" || ApplicationSettings.StandardDBMatrix[i, 6] == "" || DatabaseType != prevDatabaseType)
                            UserName = ApplicationSettings.StandardDBMatrix[i, 6];
                        if (Host == "" || ApplicationSettings.StandardDBMatrix[i, 7] == "" || DatabaseType != prevDatabaseType)
                            Host = ApplicationSettings.StandardDBMatrix[i, 7];
                        return;
                    }
            }
        }

        public String Host
        {
            get
            {
                return parameters.Host;
            }

            set
            {
                parameters.Host = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Host"));
            }
        }

        public String DatabaseName
        {
            get
            {
                return parameters.DatabaseName;
            }

            set
            {
                parameters.DatabaseName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("DatabaseName"));
            }
        }

        public String DatabaseType
        {
            get
            {
                return parameters.DatabaseType;
            }

            set
            {
                parameters.DatabaseType = value;
                OnPropertyChanged(new PropertyChangedEventArgs("DatabaseType"));
            }
        }

        public String ProviderType
        {
            get
            {
                return parameters.ProviderType;
            }

            set
            {
                parameters.ProviderType = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ProviderType"));
            }
        }

        public String ProviderName
        {
            get
            {
                return parameters.ProviderName;
            }

            set
            {
                parameters.ProviderName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ProviderName"));
            }
        }

        public String OleDbName
        {
            get
            {
                return parameters.OleDbName;
            }

            set
            {
                parameters.OleDbName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("OleDbName"));
            }
        }

        public String ConnectionString
        {
            get
            {
                return parameters.ConnectionString;
            }

            set
            {
                parameters.ConnectionString = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ConnectionString"));
            }
        }

        public String UserName
        {
            get
            {
                return parameters.UserName;
            }

            set
            {
                parameters.UserName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("UserName"));
            }
        }

        public String Password
        {
            get
            {
                return parameters.Password;
            }

            set
            {
                parameters.Password = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Password"));
            }
        }
    }
}
