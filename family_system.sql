/*
 Navicat MySQL Data Transfer

 Source Server         : 123
 Source Server Type    : MySQL
 Source Server Version : 80032 (8.0.32)
 Source Host           : localhost:3306
 Source Schema         : family_system

 Target Server Type    : MySQL
 Target Server Version : 80032 (8.0.32)
 File Encoding         : 65001

 Date: 20/01/2026 12:06:59
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for __efmigrationshistory
-- ----------------------------
DROP TABLE IF EXISTS `__efmigrationshistory`;
CREATE TABLE `__efmigrationshistory`  (
  `MigrationId` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ProductVersion` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`MigrationId`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for bill_templates
-- ----------------------------
DROP TABLE IF EXISTS `bill_templates`;
CREATE TABLE `bill_templates`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `FamilyId` int NOT NULL,
  `UserId` int NOT NULL,
  `CategoryId` int NULL DEFAULT NULL,
  `Name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Type` enum('income','expense') CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `DefaultAmount` decimal(12, 2) NULL DEFAULT NULL,
  `DefaultDescription` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `DefaultPaymentMethod` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `DefaultRemark` json NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeletedAt` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `IX_bill_templates_CategoryId`(`CategoryId` ASC) USING BTREE,
  INDEX `IX_bill_templates_FamilyId`(`FamilyId` ASC) USING BTREE,
  INDEX `IX_bill_templates_UserId`(`UserId` ASC) USING BTREE,
  CONSTRAINT `FK_bill_templates_categories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `categories` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `FK_bill_templates_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `families` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `FK_bill_templates_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for bills
-- ----------------------------
DROP TABLE IF EXISTS `bills`;
CREATE TABLE `bills`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `FamilyId` int NOT NULL,
  `UserId` int NOT NULL,
  `CategoryId` int NULL DEFAULT NULL,
  `Type` enum('income','expense') CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Amount` decimal(10, 2) NOT NULL,
  `Description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `PaymentMethod` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
  `BillDate` date NOT NULL,
  `Status` enum('draft','confirmed','deleted') CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeletedAt` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `IX_bills_BillDate`(`BillDate` ASC) USING BTREE,
  INDEX `IX_bills_CategoryId`(`CategoryId` ASC) USING BTREE,
  INDEX `IX_bills_FamilyId`(`FamilyId` ASC) USING BTREE,
  INDEX `IX_bills_FamilyId_BillDate`(`FamilyId` ASC, `BillDate` ASC) USING BTREE,
  INDEX `IX_bills_Status`(`Status` ASC) USING BTREE,
  INDEX `IX_bills_Type`(`Type` ASC) USING BTREE,
  INDEX `IX_bills_UserId`(`UserId` ASC) USING BTREE,
  INDEX `IX_bills_UserId_Type`(`UserId` ASC, `Type` ASC) USING BTREE,
  CONSTRAINT `FK_bills_categories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `categories` (`Id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `FK_bills_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `families` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `FK_bills_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 108 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for budgets
-- ----------------------------
DROP TABLE IF EXISTS `budgets`;
CREATE TABLE `budgets`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `FamilyId` int NOT NULL,
  `CategoryId` int NOT NULL,
  `Amount` decimal(12, 2) NOT NULL,
  `Period` enum('monthly','quarterly','yearly') CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Year` int NOT NULL,
  `Month` int NULL DEFAULT NULL,
  `UsedAmount` decimal(12, 2) NOT NULL,
  `AlertThreshold` decimal(5, 2) NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `Description` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `CreatedBy` int NOT NULL,
  `CreatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeletedAt` timestamp NULL DEFAULT NULL,
  `CreatorId` int NOT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `IX_budgets_CategoryId`(`CategoryId` ASC) USING BTREE,
  INDEX `IX_budgets_CreatorId`(`CreatorId` ASC) USING BTREE,
  INDEX `IX_budgets_FamilyId`(`FamilyId` ASC) USING BTREE,
  CONSTRAINT `FK_budgets_categories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `categories` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `FK_budgets_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `families` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `FK_budgets_Users_CreatorId` FOREIGN KEY (`CreatorId`) REFERENCES `users` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 15 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for categories
-- ----------------------------
DROP TABLE IF EXISTS `categories`;
CREATE TABLE `categories`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Type` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ParentId` int NULL DEFAULT NULL,
  `Icon` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Color` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `SortOrder` int NOT NULL,
  `IsSystem` tinyint(1) NOT NULL,
  `FamilyId` int NULL DEFAULT NULL,
  `Description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedAt` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime NOT NULL,
  `DeletedAt` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `IX_categories_FamilyId`(`FamilyId` ASC) USING BTREE,
  INDEX `IX_categories_ParentId`(`ParentId` ASC) USING BTREE,
  INDEX `IX_categories_Type`(`Type` ASC) USING BTREE,
  CONSTRAINT `FK_categories_categories_ParentId` FOREIGN KEY (`ParentId`) REFERENCES `categories` (`Id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `FK_categories_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `families` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 26 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for families
-- ----------------------------
DROP TABLE IF EXISTS `families`;
CREATE TABLE `families`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `InviteCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatorId` int NOT NULL,
  `BudgetCycle` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MemberLimit` int NULL DEFAULT NULL,
  `Settings` json NOT NULL,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime(6) NOT NULL,
  `DeletedAt` datetime NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `IX_Families_InviteCode`(`InviteCode` ASC) USING BTREE,
  INDEX `IX_Families_CreatorId`(`CreatorId` ASC) USING BTREE,
  INDEX `IX_Families_Status`(`Status` ASC) USING BTREE,
  CONSTRAINT `FK_Families_Users_CreatorId` FOREIGN KEY (`CreatorId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 8 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for family_members
-- ----------------------------
DROP TABLE IF EXISTS `family_members`;
CREATE TABLE `family_members`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `FamilyId` int NOT NULL,
  `UserId` int NOT NULL,
  `Role` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Permissions` json NOT NULL,
  `Nickname` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `JoinedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `LeftAt` datetime NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `IX_family_members_FamilyId_UserId`(`FamilyId` ASC, `UserId` ASC) USING BTREE,
  INDEX `IX_family_members_FamilyId`(`FamilyId` ASC) USING BTREE,
  INDEX `IX_family_members_UserId`(`UserId` ASC) USING BTREE,
  CONSTRAINT `FK_family_members_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `families` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `FK_family_members_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 12 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for family_stats
-- ----------------------------
DROP TABLE IF EXISTS `family_stats`;
CREATE TABLE `family_stats`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `FamilyId` int NOT NULL,
  `TotalIncome` decimal(12, 2) NOT NULL,
  `TotalExpense` decimal(12, 2) NOT NULL,
  `LastUpdated` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `IX_family_stats_FamilyId`(`FamilyId` ASC) USING BTREE,
  CONSTRAINT `FK_family_stats_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `families` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 8 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for notification_templates
-- ----------------------------
DROP TABLE IF EXISTS `notification_templates`;
CREATE TABLE `notification_templates`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Title` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Content` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Type` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedBy` int NULL DEFAULT NULL,
  `CreatedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime(6) NOT NULL,
  `CreatorId` int NOT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `IX_notification_templates_Code`(`Code` ASC) USING BTREE,
  INDEX `IX_notification_templates_CreatorId`(`CreatorId` ASC) USING BTREE,
  CONSTRAINT `FK_notification_templates_Users_CreatorId` FOREIGN KEY (`CreatorId`) REFERENCES `users` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for notifications
-- ----------------------------
DROP TABLE IF EXISTS `notifications`;
CREATE TABLE `notifications`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `TemplateId` int NULL DEFAULT NULL,
  `FamilyId` int NULL DEFAULT NULL,
  `UserId` int NULL DEFAULT NULL,
  `Title` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Message` text CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `IX_notifications_CreatedAt`(`CreatedAt` ASC) USING BTREE,
  INDEX `IX_notifications_FamilyId`(`FamilyId` ASC) USING BTREE,
  INDEX `IX_notifications_Status`(`Status` ASC) USING BTREE,
  INDEX `IX_notifications_TemplateId`(`TemplateId` ASC) USING BTREE,
  INDEX `IX_notifications_UserId`(`UserId` ASC) USING BTREE,
  CONSTRAINT `FK_notifications_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `families` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `FK_notifications_notification_templates_TemplateId` FOREIGN KEY (`TemplateId`) REFERENCES `notification_templates` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `FK_notifications_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 10 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for users
-- ----------------------------
DROP TABLE IF EXISTS `users`;
CREATE TABLE `users`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `OpenId` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `Nickname` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `AvatarUrl` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Phone` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `Gender` int NULL DEFAULT NULL,
  `LastLoginAt` datetime NULL DEFAULT NULL,
  `LoginCount` int NOT NULL,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Settings` json NOT NULL,
  `CreatedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime(6) NOT NULL,
  `DeletedAt` datetime NULL DEFAULT NULL,
  `PasswordHash` longblob NOT NULL,
  `PasswordSalt` longblob NOT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `IX_Users_OpenId`(`OpenId` ASC) USING BTREE,
  UNIQUE INDEX `IX_Users_Email`(`Email` ASC) USING BTREE,
  INDEX `IX_Users_Phone`(`Phone` ASC) USING BTREE,
  INDEX `IX_Users_Status`(`Status` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 15 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;
