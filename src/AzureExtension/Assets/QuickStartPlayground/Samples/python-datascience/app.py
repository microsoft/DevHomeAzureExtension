import numpy as np
import pandas as pd
from sklearn.datasets import load_iris
from sklearn.model_selection import train_test_split
from sklearn.neighbors import KNeighborsClassifier

# loading the iris dataset
iris_dataset = load_iris()

# creating a dataframe from the dataset
df = pd.DataFrame(iris_dataset.data, columns=iris_dataset.feature_names)

# adding the target column
df['target'] = iris_dataset.target

# splitting the data into training and testing sets
X_train, X_test, y_train, y_test = train_test_split(df[iris_dataset.feature_names], df['target'], random_state=0)

# creating the model
knn = KNeighborsClassifier(n_neighbors=1)

# fitting the model
knn.fit(X_train, y_train)

# making predictions
y_pred = knn.predict(X_test)

# evaluating the model
print("Test set score: {:.2f}".format(np.mean(y_pred == y_test)))