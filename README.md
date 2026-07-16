# Modulbank Платёжный сервис

Сервис для проведения платёжных операций через внешнего провайдера с идемпотентностью, обработкой сбоев и постоянным хранением данных.

---

## Технологии

- **ASP.NET Core 10**
- **Entity Framework Core 10.0.10** + **Design 10.0.10**
- **Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3** (PostgreSQL)
- **Microsoft.AspNetCore.OpenApi 10.0.9**
- **Swashbuckle.AspNetCore (Swagger) 10.2.3**
- **AutoMapper 16.2.0**
- **FluentValidation 12.1.1**
- **Docker & Docker Compose**

---

## Архитектура

Проект построен по принципам **Чистой архитектуры** с разделением на слои:
```
📦 Domain
   └── Entities
   │   ├── Operation.cs          # Агрегат (Id, Status, Amount, RowVersion)
   │   └── OperationEvent.cs     # Событие (EventId, FromStatus, ToStatus)
   └── Enums
       └── OperationStatus.cs    # CREATED, PROCESSING, COMPLETED, REJECTED

📦 Application
   └── DTOs
   │   ├── CreateOperationRequest.cs
   │   ├── OperationResponse.cs
   │   ├── OperationEventResponse.cs
   │   └── ReceiptRequest.cs
   └── Exceptions
   │   ├── ConflictException.cs
   │   └── NotFoundException.cs
   └── Interfaces
   │   ├── IOperationService.cs
   │   └── IOperationRepository.cs
   └── Services
       └── OperationService.cs   # Бизнес-логика с конкурентностью

📦 Infrastructure
   └── Persistence
   │   ├── PaymentsDbContext.cs  # EF Core + Fluent API
   │   └── Migrations            # Миграции PostgreSQL
   └── Repositories
       └── OperationRepository.cs # Реализация IOperationRepository

📦 PaymentService (Web API)
   └── Background
   │   └── ProviderSubmissionBackgroundService.cs # Восстановление отправок
   └── Controllers
   │   └── OperationController.cs # Все REST эндпоинты
   └── Middleware
   │   └── ExceptionHandlingMiddleware.cs # Глобальная обработка ошибок
   └── appsettings.json           # Конфигурация
   └── Dockerfile                 # Сборка образа
   └── Program.cs                 # Настройка DI + автоматические миграции
```

## Поток обработки операции
```
┌─────────────────────────────────────────────────────────────┐
│ 1. CREATE OPERATION │
│ POST /operations │
│ → Returns: 201 Created (operationId, status: CREATED) │
│ → Duplicate: 409 Conflict │
└─────────────────────────────────────────────────────────────┘
↓
┌─────────────────────────────────────────────────────────────┐
│ 2. SUBMIT TO PROVIDER │
│ POST /operations/{id}/submit │
│ → First call: 202 Accepted (status → PROCESSING) │
│ → Repeat call: 200 OK (current state) │
│ → Background service sends with Idempotency-Key │
└─────────────────────────────────────────────────────────────┘
↓
┌─────────────────────────────────────────────────────────────┐
│ 3. PROVIDER CALLBACK │
│ POST /receipts │
│ → Sets providerPaymentId │
│ → Status: COMPLETED or REJECTED │
│ → Returns: 204 No Content │
│ → Late/duplicate receipts: 204 (ignored, logged) │
│ → Mismatched providerPaymentId: 409 Conflict │
└─────────────────────────────────────────────────────────────┘
↓
┌─────────────────────────────────────────────────────────────┐
│ 4. QUERY OPERATION STATUS │
│ GET /operations/{id} │
│ → Returns: Full operation details + current status │
│ │
│ GET /operations/{id}/events │
│ → Returns: Complete event history (audit trail) │
└─────────────────────────────────────────────────────────────┘
↓
┌────────────────────────────────────────────────────────────┐
│ 5. HEALTH CHECK │
│ GET /health │
│ → Returns: 200 OK │
└─────────────────────────────────────────────────────────────┘
```

## 🚀 Запуск

# Клонировать репозиторий
```bash
git clone https://github.com/your-username/modulbank-payment-service.git
cd modulbank-payment-service
```
# Запустить все сервисы
```bash
docker-compose up --build
```
# Открыть Swagger UI
```bash
http://localhost:8080/swagger
```
# Остановка контейнеров
```bash
docker-compose down
```
# Остановка + удаление томов (сброс БД)
```bash
docker-compose down -v
```
