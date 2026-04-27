import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import Index from "./pages/Index.tsx";
import Play from "./pages/Play.tsx";
import HistoryPage from "./pages/History.tsx";
import NotFound from "./pages/NotFound.tsx";
import Login from "./pages/Login.tsx";
import { AuthProvider } from "./contexts/AuthContext.tsx";
import { ChessProvider } from "./contexts/ChessContext.tsx";
import { Toaster } from "sonner";
import OnlinePlay from "./pages/OnlinePlay.tsx";

const queryClient = new QueryClient();

const App = () => (
  <QueryClientProvider client={queryClient}>
    <TooltipProvider>
      <Toaster position="top-right" richColors expand={true} closeButton />
      <Sonner />
      <BrowserRouter>
        <AuthProvider>
          <ChessProvider>
            <Routes>
              <Route path="/" element={<Login />} />
              <Route path="/home" element={<Index />} />
              <Route path="/play" element={<Play />} />
              <Route path="/online-play/:gameId" element={<OnlinePlay />} />
              <Route path="/history" element={<HistoryPage />} />
              <Route path="/login" element={<Login />} />
              {/* ADD ALL CUSTOM ROUTES ABOVE THE CATCH-ALL "*" ROUTE */}
              <Route path="*" element={<NotFound />} />
            </Routes>
          </ChessProvider>
        </AuthProvider>
      </BrowserRouter>
    </TooltipProvider>
  </QueryClientProvider>
);

export default App;

// {
//     "username": "grandmaster",
//     "email": "gm@chess.com",
//     "password": "ChessMaster2024!",
//     "confirmPassword": "ChessMaster2024!"
// }
