import tensorflow as tf
import numpy as np

# create data
x_data = np.random.rand(100).astype(np.float32)
y_data = x_data * 0.1 + 0.3

# create tensorflow structure start
Weights = tf.Variable(tf.random.uniform([1], -1.0, 1.0))
biases = tf.Variable(tf.zeros([1]))

def model(x):
    return Weights * x + biases

def loss(y_true, y_pred):
    return tf.reduce_mean(tf.square(y_true - y_pred))

optimizer = tf.keras.optimizers.SGD(learning_rate=0.5)

# Training function using GradientTape
@tf.function
def train_step(x_data, y_data):
    with tf.GradientTape() as tape:
        y_pred = model(x_data)
        current_loss = loss(y_data, y_pred)
        gradients = tape.gradient(current_loss, [Weights, biases])
        optimizer.apply_gradients(zip(gradients, [Weights, biases]))
        return current_loss

# create tensorflow structure end

# Train the model
batch_size = 32
num_epochs = 201

for step in range(num_epochs):
    total_loss = 0
    for batch_start in range(0, len(x_data), batch_size):
        batch_x = x_data[batch_start:batch_start + batch_size]
        batch_y = y_data[batch_start:batch_start + batch_size]
        current_loss = train_step(batch_x, batch_y)
        total_loss += current_loss
        if step % 20 == 0:
            print(step, Weights.numpy(), biases.numpy(), total_loss.numpy())