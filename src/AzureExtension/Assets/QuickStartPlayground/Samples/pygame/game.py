import pygame

pygame.init()
screen = pygame.display.set_mode((800, 600))
playerX = 370
playerY = 480
playerX_change = 0
playerY_change = 0

running = True
while running:
    screen.fill((0, 0, 0))
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False

        if event.type == pygame.KEYDOWN:
            if event.key == pygame.K_LEFT:
                playerX_change = -1
            if event.key == pygame.K_RIGHT:
                playerX_change = 1
            if event.key == pygame.K_UP:
                playerY_change = -1
            if event.key == pygame.K_DOWN:
                playerY_change = 1

        if event.type == pygame.KEYUP:
            if event.key == pygame.K_LEFT or event.key == pygame.K_RIGHT:
                playerX_change = 0
            if event.key == pygame.K_DOWN or event.key == pygame.K_UP:
                playerY_change = 0

    playerX += playerX_change
    playerY += playerY_change
    pygame.draw.circle(screen, (0,255,0), (playerX,playerY), 20)
    pygame.display.update()