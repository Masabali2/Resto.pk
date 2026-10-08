// Global usings for the Domain project.
//
// WHY THIS FILE EXISTS:
// The requested folder layout uses singular folder names (Entities/Tenant,
// Entities/Restaurant, ...). C# forbids a namespace element with the same name
// as its member type (RestaurantSaaS.Domain.Entities.Tenant.Tenant => CS0542),
// so those namespaces are pluralized (Tenants, Restaurants, Files, ...).
// This file restores the *conceptual* single-name access everywhere in the
// solution via global using aliases, so consuming code can write
// `Tenant`, `Restaurant`, `FileMetadata` without qualification and without
// knowing about the pluralization detail.

global using RestaurantSaaS.Domain.Common;
global using RestaurantSaaS.Domain.Enums;
global using RestaurantSaaS.Domain.ValueObjects;

global using Tenant = RestaurantSaaS.Domain.Entities.Tenants.Tenant;
global using StaffMembership = RestaurantSaaS.Domain.Entities.Tenants.StaffMembership;
global using Restaurant = RestaurantSaaS.Domain.Entities.Restaurants.Restaurant;
global using RestaurantAsset = RestaurantSaaS.Domain.Entities.Restaurants.RestaurantAsset;
global using SubscriptionPlan = RestaurantSaaS.Domain.Entities.Subscriptions.SubscriptionPlan;
global using Subscription = RestaurantSaaS.Domain.Entities.Subscriptions.Subscription;
global using Menu = RestaurantSaaS.Domain.Entities.Menus.Menu;
global using MenuCategory = RestaurantSaaS.Domain.Entities.Menus.MenuCategory;
global using MenuItem = RestaurantSaaS.Domain.Entities.Menus.MenuItem;
global using WebsiteTemplate = RestaurantSaaS.Domain.Entities.Websites.WebsiteTemplate;
global using Website = RestaurantSaaS.Domain.Entities.Websites.Website;
global using WebsiteConfiguration = RestaurantSaaS.Domain.Entities.Websites.WebsiteConfiguration;
global using Customer = RestaurantSaaS.Domain.Entities.Customers.Customer;
global using Cart = RestaurantSaaS.Domain.Entities.Carts.Cart;
global using CartItem = RestaurantSaaS.Domain.Entities.Carts.CartItem;
global using Order = RestaurantSaaS.Domain.Entities.Orders.Order;
global using OrderItem = RestaurantSaaS.Domain.Entities.Orders.OrderItem;
global using OrderStatusHistory = RestaurantSaaS.Domain.Entities.Orders.OrderStatusHistory;
global using Notification = RestaurantSaaS.Domain.Entities.Notifications.Notification;
global using FileMetadata = RestaurantSaaS.Domain.Entities.Files.FileMetadata;
global using QRCode = RestaurantSaaS.Domain.Entities.QRCodes.QRCode;
global using AuditLog = RestaurantSaaS.Domain.Entities.Audit.AuditLog;
global using AppUser = RestaurantSaaS.Domain.Entities.Identity.User;
global using AppRole = RestaurantSaaS.Domain.Entities.Identity.Role;
