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
-- Table structure for table `outputhistory`
--

DROP TABLE IF EXISTS `outputhistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `outputhistory` (
  `Inverter_Id` mediumint(8) unsigned NOT NULL,
  `OutputTime` datetime NOT NULL,
  `OutputKwh` double NOT NULL,
  `Duration` mediumint(8) unsigned NOT NULL,
  `ExtractResult_Id` mediumint(8) unsigned DEFAULT NULL,
  PRIMARY KEY (`Inverter_Id`,`OutputTime`),
  UNIQUE KEY `OutputTime` (`OutputTime`,`Inverter_Id`),
  KEY `fk_OutputHistory_Inverter` (`Inverter_Id`),
  KEY `fk_OutputHistory_ExtractResult1` (`ExtractResult_Id`),
  CONSTRAINT `fk_OutputHistory_ExtractResult1` FOREIGN KEY (`ExtractResult_Id`) REFERENCES `extractresult` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `fk_OutputHistory_Inverter1` FOREIGN KEY (`Inverter_Id`) REFERENCES `inverter` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
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
