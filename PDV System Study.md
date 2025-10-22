# Workflow



Offline:



User interacts with Avalonia UI → Data stored in SQLite.



Online:



Background sync service pushes SQLite changes → Remote PostgreSQL (via Web API).



Fetches new updates from PostgreSQL → Updates local SQLite.



Conflict Resolution Strategy:



Last write wins (simple, but risk of overwriting).



Merge changes (requires more logic).





# Tools 

* Avalonia UI → for frontend.
* ASP.NET Core Web API → for backend sync endpoints.
* EF Core → ORM for both local \& remote DB.
* SQLite (offline) + PostgreSQL/MySQL (remote).
* AutoMapper → map DTOs between API and DB entities.
* BackgroundWorker/Quartz.NET → schedule sync jobs.





Frontend: Avalonia (MVVM).



Backend: ASP.NET Core API with EF Core.



Local DB: SQLite.



Remote DB: PostgreSQL (hosted in cloud).



Sync: Custom EF Core + API sync layer.

