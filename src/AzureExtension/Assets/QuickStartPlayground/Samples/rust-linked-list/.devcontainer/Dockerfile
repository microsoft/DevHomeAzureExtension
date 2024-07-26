FROM mcr.microsoft.com/devcontainers/base:ubuntu

ENV DEBIAN_FRONTEND noninteractive

RUN apt update && \
apt install -y curl && build-essential \
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y