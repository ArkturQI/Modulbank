# Modulbank Платёжный сервис

## Технологии

- ASP.NET Core 10
- Entity Framework Core 10.0.10
- Entity Framework Core Design 10.0.10
- Microsoft.AspNetCore.OpenApi 10.0.9
- Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3
- FluentValidation 12.1.1
- AutoMapper 16.2.0
- Swashbuckle.AspNetCore (Swagger) 10.2.3


##Архитектура
Проект построен по принципам чистой архитектуры (Clean Architecture) с разделением на слои:
```
   Domain                   
   └── Entities              # Сущности (Operation, OperationEvent)
   └── Enums                 # OperationStatus (CREATED, PROCESSING, COMPLETED, REJECTED)

   Infrastructure           
   └── Persistence           # AppDbContext, конфигурация EF Core
   └── Migrations            # Миграции PostgreSQL
   └── Repositories          # Реализация IOperationRepository

    PaymentService          
   └── Controllers           # OperationController (все эндпоинты)
   └── Middleware            # Глобальная обработка ошибок
   └── appsettings.json      # Конфигурация
   └── Dockerfile            # Сборка образа
   └── Program.cs            # Настройка хоста и DI

   Application 
   └── DTOs                  # CreateOperationRequest, ReceiptRequest, OperationResponse
   └── Interfaces            # IOperationService, IOperationRepository
   └── Services              # OperationService (бизнес-логика)
```

## Поток обработки операции

1. POST /operations          → CREATED (сохранение в БД)
2. POST /{id}/submit         → PROCESSING (отправка провайдеру)
3. POST /payments (provider) → Idempotency-Key: operationId
4. POST /receipts (callback) → COMPLETED / REJECTED
5. GET /operations/{id}      → Финальный статус

----------------------------------------------------------------
✅ Идемпотентность через Idempotency-Key
----------------------------------------------------------------
✅ Оптимистичная обработка конкурентных запросов
----------------------------------------------------------------
✅ Постоянное хранилище (PostgreSQL) с переживанием перезапусков
----------------------------------------------------------------
✅ Callback-квитанции определяют финальный статус
----------------------------------------------------------------
✅ Обработка ранних и повторных квитанций
----------------------------------------------------------------

🚀 Запуск

1. git clone
```bash
git clone https://github.com/your-username/modulbank-payment-service.git
cd modulbank-payment-service
```

2. Запуск Docker Compose
```bash
docker-compose up --build
```

3. Swagger UI
```bash
http://localhost:8080/swagger
```

4. Остановка и очистка
```bash
docker-compose down        # Остановить контейнеры
docker-compose down -v     # Остановить и удалить тома с БД
```

