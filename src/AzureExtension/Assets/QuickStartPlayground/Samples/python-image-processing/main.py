import cv2
import numpy as np

# Make a blank image
height = 300
width = 300
img = np.zeros((height, width, 3), np.uint8)

# Draw a circle for the face
cv2.circle(img, (width//2, height//2), 100, (255, 255, 0), -1)

# Draw the eyes
cv2.circle(img, (width//2 - 50, height//2 - 30), 20, (0, 0, 0), -1)
cv2.circle(img, (width//2 + 50, height//2 - 30), 20, (0, 0, 0), -1)

# Draw the mouth
cv2.ellipse(img, (width//2, height//2 + 30), (50, 25), 0, 0, 180, (0, 0, 0), -1)

# Clone the original image
clone_img = img.copy()

# Convert to grayscale
gray = cv2.cvtColor(clone_img, cv2.COLOR_BGR2GRAY)

# Blur the image
blur = cv2.GaussianBlur(gray, (5, 5), 0)

# Threshold the image
ret, thresh = cv2.threshold(blur, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)

# Find the contours
contours, hierarchy = cv2.findContours(thresh, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)

# Draw the contours on the cloned image
cv2.drawContours(clone_img, contours, -1, (0, 0, 255), 3)

# Show the cloned image with contours
cv2.imshow('Cloned Image with Contours', clone_img)
cv2.imshow('Original image', img)
cv2.waitKey(0)
cv2.destroyAllWindows()