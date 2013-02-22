CREATE DATABASE  IF NOT EXISTS `pvhistory` /*!40100 DEFAULT CHARACTER SET latin1 */;
USE `pvhistory`;
-- MySQL dump 10.13  Distrib 5.1.40, for Win32 (ia32)
--
-- Host: localhost    Database: pvhistory
-- ------------------------------------------------------
-- Server version	5.1.51-community

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `inverter`
--

DROP TABLE IF EXISTS `inverter`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `inverter` (
  `Id` mediumint(8) unsigned NOT NULL AUTO_INCREMENT,
  `SerialNumber` varchar(45) NOT NULL,
  `Location` varchar(255) DEFAULT NULL,
  `inverterType_Id` mediumint(8) unsigned NOT NULL,
  `InverterManager_Id` mediumint(8) unsigned DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `InverterIdentity` (`SerialNumber`,`inverterType_Id`),
  KEY `fk_Inverter_InverterManager1` (`InverterManager_Id`),
  KEY `fk_Inverter_invertertype` (`inverterType_Id`),
  CONSTRAINT `fk_Inverter_InverterManager1` FOREIGN KEY (`InverterManager_Id`) REFERENCES `invertermanager` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `fk_Inverter_invertertype` FOREIGN KEY (`inverterType_Id`) REFERENCES `invertertype` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2010-11-25 13:19:19
