# 4.2.0.3

### Startup
- Plugin init runs in `LoadAsync` instead of the constructor, so Dalamud boot no longer hitches on DI setup and lifecycle start