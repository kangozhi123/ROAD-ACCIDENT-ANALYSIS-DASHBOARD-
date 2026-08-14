from flask import Flask, request, render_template
import pickle
import numpy as np

app = Flask(__name__)
# Load the model you saved earlier
model = pickle.load(open('accident_model.pkl', 'rb'))

@app.route('/')
def home():
    return render_template('index.html')

@app.route('/predict', methods=['POST'])
def predict():
    try:
        features = [float(x) for x in request.form.values()]
        final_features = np.array(features).reshape(1, -1)
        prediction = model.predict(final_features)
        prediction_text = 'Predicted Severity: {}'.format(prediction[0])
    except Exception as e:
        prediction_text = f'Error processing prediction: {e}'

    return render_template('index.html', prediction_text=prediction_text)

if __name__ == '__main__':
    app.run(debug=True)