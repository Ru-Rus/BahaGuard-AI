# Stage 1: Build Angular Application
FROM node:22-alpine AS build
WORKDIR /app

# Copy dependency specifications and install
COPY package*.json ./
RUN npm ci

# Copy full frontend source code and compile production artifacts
COPY . .
RUN npm run build -- --configuration=production

# Stage 2: Serve via Nginx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
