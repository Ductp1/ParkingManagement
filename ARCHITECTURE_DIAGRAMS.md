# 📐 NotificationService - Architecture Diagrams

## 1️⃣ System Context Diagram (C4 Level 1)

```mermaid
graph TB
	Client["📱 Mobile/Web Client<br/>(React, iOS, Android)"]
	Gateway["🚪 API Gateway<br/>(Ocelot)"]
	NotifService["🔔 NotificationService"]
	BookingService["📅 BookingService"]
	UserService["👤 UserService"]
	ParkingService["🅿️ ParkingService"]
	Database["🗄️ PM_NotificationDb<br/>(SQL Server)"]
	ExternalServices["☁️ External Services<br/>(SMTP, FCM, Twilio)"]
	MessageBroker["📬 Message Broker<br/>(RabbitMQ)"]

	Client -->|HTTP/WebSocket| Gateway
	Gateway -->|REST API| NotifService
	Gateway -->|REST API| BookingService
	Gateway -->|REST API| UserService
	Gateway -->|REST API| ParkingService

	BookingService -->|Publish Event| MessageBroker
	UserService -->|Publish Event| MessageBroker
	ParkingService -->|Publish Event| MessageBroker

	MessageBroker -->|Subscribe| NotifService

	NotifService -->|Read/Write| Database
	NotifService -->|Send Email| ExternalServices
	NotifService -->|Send SMS| ExternalServices
	NotifService -->|Send Push| ExternalServices

	NotifService -->|WebSocket/SignalR| Client
```

---

## 2️⃣ Container Diagram (C4 Level 2)

```mermaid
graph TB
	Browser["🌐 Browser"]
	MobileApp["📱 Mobile App"]

	subgraph API["API Gateway (Ocelot)"]
		GatewayProxy["Proxy Router"]
	end

	subgraph NotifService["🔔 NotificationService"]
		API["REST API<br/>/api/v1/notifications"]
		SignalRHub["SignalR Hub<br/>/hubs/notify"]
		AppLayer["Application Layer<br/>Use Cases"]
		DomainLayer["Domain Layer<br/>Entities"]
		InfraLayer["Infrastructure Layer<br/>EF Core, Repositories"]
	end

	subgraph Database["Data Store"]
		SQLServer["PM_NotificationDb<br/>(SQL Server)"]
	end

	subgraph ExternalServices["External Services"]
		SMTP["📧 SMTP<br/>(Email)"]
		FCM["🔥 Firebase FCM<br/>(Push)"]
		SNS["📲 AWS SNS<br/>(SMS)"]
	end

	Browser -->|HTTP| GatewayProxy
	MobileApp -->|HTTP/WebSocket| GatewayProxy

	GatewayProxy -->|Route| API
	GatewayProxy -->|WebSocket| SignalRHub

	API -->|Call| AppLayer
	SignalRHub -->|Notify| AppLayer

	AppLayer -->|Use| DomainLayer
	AppLayer -->|Use| InfraLayer

	InfraLayer -->|ORM| SQLServer
	InfraLayer -->|Send via| SMTP
	InfraLayer -->|Send via| FCM
	InfraLayer -->|Send via| SNS

	SignalRHub -->|Real-time| Browser
	SignalRHub -->|Real-time| MobileApp
```

---

## 3️⃣ Notification Sending Flow

```mermaid
sequenceDiagram
	actor User as User
	participant Client as Client App
	participant API as NotifService API
	participant Dispatcher as Dispatcher
	participant EmailSender as EmailSender
	participant SmsSender as SmsSender
	participant PushSender as PushSender
	participant SignalRHub as SignalRHub
	participant Database as Database

	Note over User,Database: Step 1: Send Notification Request
	Client->>API: POST /send {userId, channel, title, body}
	API->>Dispatcher: SendAsync(request)

	Note over User,Database: Step 2: Route to Correct Channel
	alt Channel = Email
		Dispatcher->>EmailSender: SendAsync(notification)
		EmailSender->>Database: Save (status=Sent)
	else Channel = SMS
		Dispatcher->>SmsSender: SendAsync(notification)
		SmsSender->>Database: Save (status=Sent)
	else Channel = Push
		Dispatcher->>PushSender: SendAsync(notification)
		PushSender->>Database: Save (status=Sent)
	else Channel = InApp
		Dispatcher->>SignalRHub: BroadcastAsync(notification)
		SignalRHub->>Database: Save (status=Sent)
	end

	Note over User,Database: Step 3: Deliver to User
	alt InApp
		SignalRHub-->>Client: Real-time message
		Client-->>User: 🔔 Popup notification
	else Email/SMS/Push
		Database-->>User: 📧 Email / 📱 SMS / 🔥 Push
	end

	Note over User,Database: Step 4: User Marks as Read
	User->>Client: Click notification
	Client->>API: PUT /{id}/read
	API->>Database: Update (isRead=true)
```

---

## 4️⃣ Use Case Diagram (Actor Model)

```mermaid
graph TB
	User["👤 User"]
	Admin["🔐 Admin"]
	System["🔄 System<br/>(BookingService, etc)"]

	subgraph UseCases["NotificationService Use Cases"]
		GetInbox["Get Inbox<br/>(List Notifications)"]
		MarkRead["Mark as Read"]
		RegDevice["Register Device Token"]
		UnregDevice["Unregister Device Token"]
		GetPrefs["Get Preferences"]
		UpdatePrefs["Update Preferences"]
		SendNotif["Send Notification"]
		RetryNotif["Retry Notification"]
	end

	subgraph S1["Sprint 1 (S1)"]
		GetInbox
		RegDevice
		SendNotif
	end

	subgraph S2["Sprint 2 (S2)"]
		MarkRead
	end

	subgraph S4["Sprint 4 (S4)"]
		GetPrefs
		UpdatePrefs
	end

	User -->|T601| GetInbox
	User -->|T601| RegDevice
	User -->|S2| MarkRead
	User -->|S4| GetPrefs
	User -->|S4| UpdatePrefs

	Admin -->|T602| SendNotif
	Admin -->|T602| UnregDevice

	System -->|T602| SendNotif
	System -->|T602| RetryNotif
```

---

## 5️⃣ Component Diagram (NotificationService Internals)

```mermaid
graph TB
	subgraph API_Layer["🌐 API Layer"]
		NotifController["NotificationsController<br/>- GET / (inbox)<br/>- PUT /{id}/read<br/>- POST /send<br/>- GET /preferences<br/>- PUT /preferences<br/>- POST /devices<br/>- DELETE /devices"]
		SignalRHubs["SignalR Hubs<br/>- NotificationHub<br/>- ParkingHub"]
	end

	subgraph App_Layer["⚙️ Application Layer"]
		Inbox["GetInboxUseCase"]
		List["GetNotificationsUseCase"]
		MarkRead["MarkNotificationAsReadUseCase"]
		Send["SendNotificationUseCase"]
		Retry["RetryNotificationUseCase"]
		RegDevice["RegisterDeviceTokenUseCase"]
		UnregDevice["UnregisterDeviceTokenUseCase"]
		GetPrefs["GetNotificationPreferencesUseCase"]
		UpdatePrefs["UpdateNotificationPreferencesUseCase"]
	end

	subgraph Domain_Layer["🎯 Domain Layer"]
		Notification["Notification Entity"]
		DeviceToken["DeviceToken Entity"]
		Preference["NotificationPreference Entity"]
		Template["NotificationTemplate Entity"]
	end

	subgraph Infra_Layer["🔧 Infrastructure Layer"]
		DbContext["NotificationDbContext<br/>(EF Core)"]
		NotifRepo["NotificationRepository"]
		DeviceRepo["DeviceTokenRepository"]
		PrefRepo["PreferenceRepository"]
		Configuration["Entity Configurations"]
	end

	subgraph Senders["📤 Channel Senders"]
		EmailSender["EmailSender<br/>(SMTP)"]
		SmsSender["SmsSender<br/>(Twilio/SNS)"]
		PushSender["PushSender<br/>(FCM)"]
		InAppSender["InAppSender<br/>(SignalR)"]
	end

	subgraph External["☁️ External Services"]
		SMTPServer["SMTP Server"]
		FCMService["Firebase FCM"]
		TwilioService["Twilio/AWS SNS"]
	end

	subgraph Data["🗄️ Data Layer"]
		Database["PM_NotificationDb<br/>(SQL Server)"]
	end

	NotifController -->|Call| Inbox
	NotifController -->|Call| List
	NotifController -->|Call| MarkRead
	NotifController -->|Call| Send
	NotifController -->|Call| GetPrefs
	NotifController -->|Call| UpdatePrefs

	SignalRHubs -->|Call| Send
	SignalRHubs -->|Call| RegDevice

	Inbox -->|Query| NotifRepo
	List -->|Query| NotifRepo
	MarkRead -->|Update| NotifRepo
	Send -->|Create| NotifRepo
	Retry -->|Update| NotifRepo

	RegDevice -->|Create| DeviceRepo
	UnregDevice -->|Update| DeviceRepo

	GetPrefs -->|Query| PrefRepo
	UpdatePrefs -->|Update| PrefRepo

	NotifRepo -->|Use| DbContext
	DeviceRepo -->|Use| DbContext
	PrefRepo -->|Use| DbContext

	DbContext -->|Map| Notification
	DbContext -->|Map| DeviceToken
	DbContext -->|Map| Preference
	DbContext -->|Map| Template

	DbContext -->|Apply| Configuration

	Send -->|Route| EmailSender
	Send -->|Route| SmsSender
	Send -->|Route| PushSender
	Send -->|Route| InAppSender

	EmailSender -->|Send| SMTPServer
	PushSender -->|Send| FCMService
	SmsSender -->|Send| TwilioService
	InAppSender -->|Broadcast| SignalRHubs

	NotifRepo -->|Persist| Database
	DeviceRepo -->|Persist| Database
	PrefRepo -->|Persist| Database
```

---

## 6️⃣ Database Schema Diagram

```mermaid
graph TB
	subgraph "NotificationService Schema"
		Notifications["📋 Notifications<br/>------<br/>IdPK<br/>UserId<br/>TemplateIdFK<br/>Channel nvarchar<br/>TemplateKey<br/>Title<br/>Body<br/>DataJson<br/>Status nvarchar<br/>RetryCount<br/>LastError<br/>SentAtUtc<br/>IsRead bit<br/>ReadAtUtc<br/>CreatedAtUtc<br/>UpdatedAtUtc"]

		Templates["📄 NotificationTemplates<br/>------<br/>IdPK<br/>Key<br/>Channel<br/>TitleTemplate<br/>BodyTemplate<br/>IsActive<br/>CreatedAtUtc<br/>UpdatedAtUtc"]

		Devices["📱 DeviceTokens<br/>------<br/>IdPK<br/>UserId<br/>Token<br/>Platform nvarchar<br/>DeviceName<br/>LastUsedAtUtc<br/>IsRevoked<br/>CreatedAtUtc<br/>UpdatedAtUtc"]

		Preferences["⚙️ NotificationPreferences<br/>------<br/>IdPK<br/>UserId<br/>TemplateKey<br/>Channel<br/>IsEnabled bit<br/>QuietFrom time<br/>QuietTo time<br/>CreatedAtUtc<br/>UpdatedAtUtc"]

		Outbox["📮 OutboxMessages<br/>(Outbox Pattern)<br/>------<br/>IdPK Guid<br/>EventType<br/>PayloadJson<br/>OccurredAtUtc<br/>ProcessedAtUtc<br/>AttemptCount<br/>LastError"]
	end

	Notifications -->|FK TemplateId| Templates
	Notifications -->|Index: UserId, IsRead| Notifications
	Notifications -->|Index: Status, RetryCount| Notifications
	Devices -->|Index: UserId, IsRevoked| Devices
	Preferences -->|Index: UserId, TemplateKey, Channel| Preferences

	style Notifications fill:#e1f5ff
	style Templates fill:#f3e5f5
	style Devices fill:#e8f5e9
	style Preferences fill:#fff3e0
	style Outbox fill:#fce4ec
```

---

## 7️⃣ Deployment Diagram (Docker & Kubernetes)

```mermaid
graph TB
	subgraph K8s["Kubernetes Cluster"]
		subgraph Pod1["NotificationService Pod-1"]
			API1["API Container<br/>Port 8080"]
			SignalR1["SignalR<br/>WebSocket 8080"]
		end

		subgraph Pod2["NotificationService Pod-2"]
			API2["API Container<br/>Port 8080"]
			SignalR2["SignalR<br/>WebSocket 8080"]
		end

		LoadBalancer["⚖️ Load Balancer<br/>Service<br/>Sticky Sessions"]

		subgraph Database["Database Pod"]
			SQLServer["SQL Server 2022<br/>Persistent Volume"]
		end

		subgraph Cache["Cache Pod"]
			Redis["Redis Cache<br/>Device Tokens<br/>Preferences"]
		end

		subgraph MessageBroker["Message Broker"]
			RabbitMQ["RabbitMQ<br/>Notification Queue"]
		end
	end

	subgraph External["External Services"]
		SMTP["SMTP Server"]
		FCM["Firebase FCM"]
		Twilio["Twilio/SNS"]
	end

	Client["Client<br/>(Browser/Mobile)"]

	Client -->|HTTP/WebSocket| LoadBalancer
	LoadBalancer -->|Route| Pod1
	LoadBalancer -->|Route| Pod2

	Pod1 -->|Query/Insert| SQLServer
	Pod2 -->|Query/Insert| SQLServer

	Pod1 -->|Cache| Redis
	Pod2 -->|Cache| Redis

	Pod1 -->|Consume| RabbitMQ
	Pod2 -->|Consume| RabbitMQ

	Pod1 -->|Send| SMTP
	Pod1 -->|Send| FCM
	Pod1 -->|Send| Twilio

	Pod2 -->|Send| SMTP
	Pod2 -->|Send| FCM
	Pod2 -->|Send| Twilio
```

---

## 8️⃣ State Machine: Notification Lifecycle

```mermaid
stateDiagram-v2
	[*] --> Created: POST /send

	Created --> Pending: Validate request

	Pending --> Sending: Start dispatch

	Sending --> Sent: ✅ Send success
	Sending --> Failed: ❌ Send error

	Failed --> Retrying: Retry attempt\n(RetryCount < 3)
	Failed --> DeadLetter: Max retries\nexceeded

	Retrying --> Sent: ✅ Retry success
	Retrying --> Failed: ❌ Retry failed

	Sent --> Read: User opens\nnotification
	Sent --> Deleted: Auto-delete\nafter 30 days

	Read --> Deleted: Auto-delete\nafter 30 days

	DeadLetter --> ManualReview: Admin review
	ManualReview --> Sent: Manual send
	ManualReview --> Deleted: Manual delete

	Deleted --> [*]

	style Sent fill:#90EE90
	style Failed fill:#FFB6C6
	style Retrying fill:#FFE4B5
	style DeadLetter fill:#FF6347
	style Created fill:#87CEEB
	style Pending fill:#87CEEB
```

---

## 9️⃣ Message Flow: Event-Driven Notification

```mermaid
graph LR
	subgraph "Source Service"
		BookingService["📅 BookingService<br/>Event: BookingCreated"]
	end

	MB["📬 RabbitMQ<br/>Exchange: notifications.exchange<br/>Queue: notifications.queue"]

	subgraph "NotificationService"
		Consumer["Consumer<br/>(Background Worker)"]
		Parser["Event Parser"]
		Dispatcher["Dispatcher"]
	end

	Channels["Channel Senders<br/>SMTP/FCM/SNS"]

	User["👤 User"]

	BookingService -->|Publish<br/>BookingCreated| MB
	MB -->|Subscribe| Consumer
	Consumer -->|Parse| Parser
	Parser -->|Extract Data| Dispatcher
	Dispatcher -->|Route Channel| Channels
	Channels -->|Send| User

	Note over ParserDispatcher: "Convert event to notification request"
```

---

## 🔟 Retry Strategy Flow

```mermaid
graph TD
	Notification["Notification Created<br/>Status = Pending"]
	Send["Send to Channel"]
	Success{Send<br/>Success?}
	Update["Update Status = Sent"]
	Failed["Update Status = Failed<br/>RetryCount++"]
	CheckRetry{RetryCount<br/>< 3?}
	Wait["Wait 5 minutes"]
	Retry["Attempt Retry<br/>RetryCount++"]
	DeadLetter["Move to Dead Letter Queue<br/>Manual Review"]
	Monitor["👨‍⚕️ Admin Dashboard<br/>Review Failed"]

	Notification --> Send
	Send --> Success

	Success -->|Yes| Update
	Update -->|✅| Done1["Notification Complete"]

	Success -->|No| Failed
	Failed --> CheckRetry

	CheckRetry -->|Yes| Wait
	Wait --> Retry

	Retry --> Send

	CheckRetry -->|No| DeadLetter
	DeadLetter --> Monitor

	Monitor -->|Manual Resend| Retry
	Monitor -->|Manual Delete| Done2["Abandoned"]
```

---

## Summary

- **8 Diagrams** covering all aspects of NotificationService
- **C4 Model** (Context, Container, Component, Code)
- **Flow Diagrams** (Sequence, State Machine, Event-Driven)
- **Infrastructure** (Deployment, Database, Architecture)

Sử dụng các diagrams này để:
1. 📚 Understand systems architecture
2. 🎓 Onboard new team members
3. 📋 Document requirements
4. 🧪 Design test scenarios
5. 🚀 Plan deployments
