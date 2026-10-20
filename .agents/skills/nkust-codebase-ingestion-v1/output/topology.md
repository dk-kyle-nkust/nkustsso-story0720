```mermaid
graph TD
    classDef host fill:#fff3e0,stroke:#e65100,stroke-width:2px,color:#bf360c;
    classDef lib fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px,color:#1b5e20;

    NkustSso.Server["NkustSso.Server"]:::lib
    NkustSso.StorageChecks["NkustSso.StorageChecks"]:::lib

    NkustSso.StorageChecks -->|references| NkustSso.Server
```
