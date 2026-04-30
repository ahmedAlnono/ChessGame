```markdown
# ♟️ Gambit - Real-Time Chess Platform

A full-stack real-time chess platform built with **.NET 9** and **React** featuring online multiplayer, matchmaking, and live gameplay via SignalR.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)
![React](https://img.shields.io/badge/React-18-61DAFB?style=flat-square&logo=react)
![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?style=flat-square&logo=typescript)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?style=flat-square&logo=postgresql)
![SignalR](https://img.shields.io/badge/SignalR-Real--time-FF6A00?style=flat-square)

---

## 📸 Screenshots

| Lobby | Game Board | Challenge |
|-------|-----------|-----------|
| Players online with challenge system | Real-time chess with live updates | Accept or decline challenges |

---

## ✨ Features

### 🎮 Game Features
- **Real-time multiplayer** via SignalR WebSockets
- **Challenge system** — challenge online players directly
- **Matchmaking** — automatic opponent finding by rating
- **AI opponent** — play against the house engine
- **Local multiplayer** — pass-and-play on same screen
- **Full chess rules** — castling, en passant, promotion, stalemate
- **Move animations** — smooth piece sliding and capture effects
- **Game chat** — communicate with your opponent in-game

### 👤 User System
- JWT authentication with refresh tokens
- User profiles with ratings and statistics
- Online/offline status indicators
- Game history with pagination
- Win/loss/draw statistics

### ⚙️ Backend Features
- Background job processing (game cleanup, matchmaking, timers)
- ELO rating system with K-factor adjustments
- Rate limiting and security middleware
- Health checks for monitoring
- PostgreSQL with Entity Framework Core
- Redis for caching and SignalR backplane
- Swagger API documentation

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────┐
│                    Frontend                      │
│              React + TypeScript                  │
│    SignalR Client  │  Axios  │  React Router    │
└────────────────────┬────────────────────────────┘
                     │
              WebSocket │ REST
                     │
┌────────────────────┴────────────────────────────┐
│                   Backend                        │
│                 ASP.NET Core 9                   │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │ Auth API │ │ Game API │ │ Matchmaking API │  │
│  └──────────┘ └──────────┘ └─────────────────┘  │
│  ┌──────────────────────────────────────────┐   │
│  │         SignalR Hubs (Real-time)          │   │
│  │    ChessHub  │  ChatHub                   │   │
│  └──────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────┐   │
│  │         Background Services               │   │
│  │  GameCleanup │ Matchmaking │ Connection   │   │
│  └──────────────────────────────────────────┘   │
└────────────────────┬────────────────────────────┘
                     │
              ┌──────┴──────┐
              │ PostgreSQL  │  Redis
              └─────────────┘
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL 17](https://www.postgresql.org/)
- [Redis 7](https://redis.io/) (optional, for scaling)

### Backend Setup

```bash
# Clone the repository
git clone https://github.com/yourusername/gambit-chess.git
cd gambit-chess

# Navigate to API project
cd src/ChessAPI

# Update connection string in appsettings.json
# "DefaultConnection": "Host=localhost;Database=ChessDB;Username=postgres;Password=yourpassword"

# Restore packages
dotnet restore

# Run database migrations
dotnet ef database update

# Start the API
dotnet run
```

The API will be available at `http://localhost:5000`

### Frontend Setup

```bash
# Navigate to React project
cd src/chess-client

# Install dependencies
npm install

# Start development server
npm run dev
```

The client will be available at `http://localhost:5173`

### Default Users (Seeded)

| Email | Password | Role |
|-------|----------|------|
| admin@chess.com | Admin123! | Admin |
| demo@chess.com | Demo123! | User |

---

## 📡 API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login |
| POST | `/api/auth/refresh` | Refresh token |
| GET | `/api/auth/me` | Get current user |

### Games
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/games` | Create game |
| GET | `/api/games/{id}` | Get game details |
| POST | `/api/games/{id}/moves` | Make a move |
| POST | `/api/games/{id}/resign` | Resign |
| POST | `/api/games/{id}/offer-draw` | Offer draw |

### SignalR Hubs
| Hub | Path | Description |
|-----|------|-------------|
| ChessHub | `/hubs/chess` | Game moves, challenges, matchmaking |
| ChatHub | `/hubs/chat` | Lobby and in-game chat |

---

## 🔧 Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ChessDB;Username=postgres;Password=yourpassword",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-at-least-32-characters",
    "ExpirationInMinutes": 60
  },
  "GameSettings": {
    "DefaultTimeControl": 600,
    "InactivityTimeoutMinutes": 30
  }
}
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_URLS` | API URLs | `http://localhost:5000` |
| `ASPNETCORE_ENVIRONMENT` | Environment | `Development` |

---

## 📂 Project Structure

```
ChessAPI/
├── src/
│   ├── ChessAPI/                    # .NET Backend
│   │   ├── Controllers/             # REST API controllers
│   │   ├── Hubs/                    # SignalR hubs
│   │   ├── Models/
│   │   │   ├── Entities/            # Database entities
│   │   │   ├── DTOs/                # Data transfer objects
│   │   │   ├── Enums/               # Enumerations
│   │   │   └── Chess/               # Chess engine
│   │   ├── Services/
│   │   │   ├── Interfaces/          # Service contracts
│   │   │   ├── Implementations/     # Service logic
│   │   │   └── BackgroundServices/  # Background jobs
│   │   ├── Middleware/              # Request pipeline
│   │   ├── Helpers/                 # Utility classes
│   │   └── Data/                    # DbContext
│   └── chess-client/                # React Frontend
│       ├── src/
│       │   ├── components/          # Reusable UI
│       │   ├── contexts/            # React contexts
│       │   ├── hooks/               # Custom hooks
│       │   ├── pages/               # Page components
│       │   ├── services/            # API & SignalR
│       │   └── store/               # Zustand store
│       └── public/
```

---

## 🛠️ Tech Stack

### Backend
- **ASP.NET Core 9** — Web framework
- **Entity Framework Core** — ORM
- **SignalR** — Real-time WebSocket communication
- **PostgreSQL** — Primary database
- **Redis** — Caching & SignalR backplane
- **JWT** — Authentication

### Frontend
- **React 18** — UI library
- **TypeScript** — Type safety
- **Vite** — Build tool
- **Tailwind CSS** — Styling
- **Zustand** — State management
- **SignalR Client** — Real-time client
- **Axios** — HTTP client
- **Sonner** — Toast notifications

---

## 🧪 Testing

```bash
# Backend tests
dotnet test

# Frontend tests
npm run test
```

---

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Chess piece symbols from Unicode
- React Chessboard library
- .NET community for excellent documentation

---

**Built with ♟️ by [Your Name]**
```

This README covers:
- Project overview and features
- Architecture diagram
- Setup instructions
- API documentation
- Configuration guide
- Project structure
- Tech stack
- Testing instructions

You can customize the GitHub links, screenshots, and your name at the bottom.
