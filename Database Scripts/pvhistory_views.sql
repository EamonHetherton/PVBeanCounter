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
-- Temporary table structure for view `dayoutput_v`
--

DROP TABLE IF EXISTS `dayoutput_v`;
/*!50001 DROP VIEW IF EXISTS `dayoutput_v`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE TABLE `dayoutput_v` (
  `Inverter_Id` mediumint(8) unsigned,
  `OutputDay` date,
  `OutputKwh` double
) ENGINE=MyISAM */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `fulldayoutput_v`
--

DROP TABLE IF EXISTS `fulldayoutput_v`;
/*!50001 DROP VIEW IF EXISTS `fulldayoutput_v`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE TABLE `fulldayoutput_v` (
  `Inverter_Id` mediumint(8) unsigned,
  `OutputDay` date,
  `OutputKwh` double
) ENGINE=MyISAM */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `fulldayoutputsummary_v`
--

DROP TABLE IF EXISTS `fulldayoutputsummary_v`;
/*!50001 DROP VIEW IF EXISTS `fulldayoutputsummary_v`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE TABLE `fulldayoutputsummary_v` (
  `OutputDay` date,
  `OutputKwh` double
) ENGINE=MyISAM */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `outputhistory_v`
--

DROP TABLE IF EXISTS `outputhistory_v`;
/*!50001 DROP VIEW IF EXISTS `outputhistory_v`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE TABLE `outputhistory_v` (
  `Inverter_Id` mediumint(8) unsigned,
  `OutputTime` datetime,
  `OutputKwh` double,
  `Duration` mediumint(8) unsigned,
  `OutputDate` date,
  `OutputTimeOfDay` time,
  `OutputKw` double
) ENGINE=MyISAM */;
SET character_set_client = @saved_cs_client;

--
-- Final view structure for view `dayoutput_v`
--

/*!50001 DROP TABLE IF EXISTS `dayoutput_v`*/;
/*!50001 DROP VIEW IF EXISTS `dayoutput_v`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8 */;
/*!50001 SET character_set_results     = utf8 */;
/*!50001 SET collation_connection      = utf8_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`PVRecords`@`%` SQL SECURITY DEFINER */
/*!50001 VIEW `dayoutput_v` AS select `oh`.`Inverter_Id` AS `Inverter_Id`,cast(`oh`.`OutputTime` as date) AS `OutputDay`,sum(`oh`.`OutputKwh`) AS `OutputKwh` from `outputhistory` `oh` group by `oh`.`Inverter_Id`,cast(`oh`.`OutputTime` as date) order by `oh`.`Inverter_Id`,cast(`oh`.`OutputTime` as date) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `fulldayoutput_v`
--

/*!50001 DROP TABLE IF EXISTS `fulldayoutput_v`*/;
/*!50001 DROP VIEW IF EXISTS `fulldayoutput_v`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8 */;
/*!50001 SET character_set_results     = utf8 */;
/*!50001 SET collation_connection      = utf8_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`PVRecords`@`%` SQL SECURITY DEFINER */
/*!50001 VIEW `fulldayoutput_v` AS select `oh`.`Inverter_Id` AS `Inverter_Id`,cast(`oh`.`OutputTime` as date) AS `OutputDay`,sum(`oh`.`OutputKwh`) AS `OutputKwh` from `outputhistory` `oh` group by `oh`.`Inverter_Id`,cast(`oh`.`OutputTime` as date) having (sum(`oh`.`Duration`) = ((60 * 60) * 24)) order by `oh`.`Inverter_Id`,cast(`oh`.`OutputTime` as date) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `fulldayoutputsummary_v`
--

/*!50001 DROP TABLE IF EXISTS `fulldayoutputsummary_v`*/;
/*!50001 DROP VIEW IF EXISTS `fulldayoutputsummary_v`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8 */;
/*!50001 SET character_set_results     = utf8 */;
/*!50001 SET collation_connection      = utf8_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`PVRecords`@`%` SQL SECURITY DEFINER */
/*!50001 VIEW `fulldayoutputsummary_v` AS select `fulldayoutput_v`.`OutputDay` AS `OutputDay`,sum(`fulldayoutput_v`.`OutputKwh`) AS `OutputKwh` from `fulldayoutput_v` group by `fulldayoutput_v`.`OutputDay` order by `fulldayoutput_v`.`OutputDay` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `outputhistory_v`
--

/*!50001 DROP TABLE IF EXISTS `outputhistory_v`*/;
/*!50001 DROP VIEW IF EXISTS `outputhistory_v`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8 */;
/*!50001 SET character_set_results     = utf8 */;
/*!50001 SET collation_connection      = utf8_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`PVRecords`@`%` SQL SECURITY DEFINER */
/*!50001 VIEW `outputhistory_v` AS select `outputhistory`.`Inverter_Id` AS `Inverter_Id`,`outputhistory`.`OutputTime` AS `OutputTime`,`outputhistory`.`OutputKwh` AS `OutputKwh`,`outputhistory`.`Duration` AS `Duration`,cast(`outputhistory`.`OutputTime` as date) AS `OutputDate`,cast(`outputhistory`.`OutputTime` as time) AS `OutputTimeOfDay`,((`outputhistory`.`OutputKwh` * 3600) / `outputhistory`.`Duration`) AS `OutputKw` from `outputhistory` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Dumping routines for database 'pvhistory'
--
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2010-11-25 13:19:20
